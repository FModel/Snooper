using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Extensions;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.UI;

namespace Snooper.Rendering.Systems;

[StructLayout(LayoutKind.Sequential)]
public struct LightData
{
    public Vector3 Position;      // World space position
    public float Range;           // Light range/radius
    public Vector3 Color;         // Light color
    public uint Type;             // 0 = point/sphere, 1 = spot, 2 = rect
    public Vector3 Direction;     // Spot light direction (world space)
    public float SpotAngle;       // Spot light inner cone angle (cosine)
    public float SpotOuterAngle;  // Spot light outer cone angle (cosine)
    public float Intensity;       // Light intensity
    public float SizeX;           // Rect light width
    public float SizeY;           // Rect light height
    public Vector3 UpVector;
    public uint UseInverseSquaredFalloff;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct ClusterAABB
{
    public readonly Vector3 MinPoint;
    public readonly float Padding1;
    public readonly Vector3 MaxPoint;
    public readonly float Padding2;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct ClusterData
{
    public readonly uint Offset;   // Offset into light index list
    public readonly uint Count;    // Number of lights in this cluster
}

public class ClusteredLightSystem : ComputeRenderSystem<LightComponent>, IMemoryDetailsProvider, IControllable, IResizable
{
    private const int TileSize = 32;
    private const int WorkGroupSize = 64;
    private const int MaxLightsPerCluster = 128;

    private abstract class LightBindings : Bindings
    {
        public const uint LightData = BaseMaxBinding + 1;
        public const uint LightClusterData = BaseMaxBinding + 2;
        public const uint LightIndexList = BaseMaxBinding + 3;
        public const uint LightClusterAabbs = BaseMaxBinding + 4;
        public const uint MaxBinding = LightClusterAabbs;

        public static readonly string[] OwnDefines =
        [
            Define("LIGHT_DATA", LightData),
            Define("LIGHT_CLUSTER_DATA", LightClusterData),
            Define("LIGHT_INDEX_LIST", LightIndexList),
            Define("LIGHT_CLUSTER_AABBS", LightClusterAabbs)
        ];
    }

    public override ActorSystemType SystemType => ActorSystemType.Custom;
    public override uint Order => 99; // at least after TransformSystem
    public override int Capacity => 10000;
    public override uint? MaxBindingUsed => LightBindings.MaxBinding;

    private readonly ShaderStorageBuffer<LightData> _lightDataBuffer = new();
    private readonly ShaderStorageBuffer<ClusterAABB> _clusterAABBBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<ClusterData> _clusterDataBuffer = new(BufferUsageHint.DynamicDraw);
    private readonly ShaderStorageBuffer<uint> _lightIndexListBuffer = new(BufferUsageHint.DynamicDraw);

    private readonly ComputeShader _clusterBuildProgram = new("Lighting/cluster_build.comp") { Defines = LightBindings.OwnDefines };
    private readonly ComputeShader _lightCullingProgram = new("Lighting/light_culling.comp")
    {
        Defines = [..LightBindings.OwnDefines, $"MAX_LIGHTS_PER_CLUSTER {MaxLightsPerCluster}"]
    };

    public int GridDimensionX { get; private set; }
    public int GridDimensionY { get; private set; }
    public int GridDimensionZ => 16;

    private int _numClusters;
    private int _numWorkGroups;
    private int _screenWidth = 1;
    private int _screenHeight = 1;

    private bool _clustersDirty = true;
    private Matrix4x4 _lastClusterProjection;

    internal DirectionalLightComponent? DirectionalLight
    {
        get;
        set
        {
            if (field == value) return;

            if (field != null) field.IsEnabled = false;
            field = value;
            if (field != null) field.IsEnabled = true;
        }
    }

    internal static string[] LightingDefines => LightBindings.OwnDefines;
    internal static int MaxLightsPerClusterLimit => MaxLightsPerCluster;

    protected override void OnLoad()
    {
        base.OnLoad();

        _lightDataBuffer.Generate();
        _lightDataBuffer.Allocate(EnqueuedComponentsCount);

        _clusterAABBBuffer.Generate();
        _clusterDataBuffer.Generate();
        _lightIndexListBuffer.Generate();

        _clusterBuildProgram.Generate();
        _clusterBuildProgram.Link();

        _lightCullingProgram.Generate();
        _lightCullingProgram.Link();

        IsEnabled = false;
    }

    protected override void OnExecute(CameraComponent camera)
    {
        if (ConsumeClustersDirty(camera))
        {
            using (Profiler.Gpu("Build")) BuildClusters(camera);
        }

        using (Profiler.Cull()) CullLights(camera);
    }

    // Cluster AABBs are camera-relative (view space) and independent of the camera's position/orientation
    // and of the lights, so they only need rebuilding when the projection or screen size changes.
    private bool ConsumeClustersDirty(CameraComponent camera)
    {
        var projection = camera.InverseProjectionMatrix;
        if (!_clustersDirty && projection == _lastClusterProjection)
            return false;

        _clustersDirty = false;
        _lastClusterProjection = projection;
        return true;
    }

    private void BuildClusters(CameraComponent camera)
    {
        if (_numClusters == 0) return;

        _clusterBuildProgram.Use();
        _clusterBuildProgram.SetUniform("uScreenWidth", _screenWidth);
        _clusterBuildProgram.SetUniform("uScreenHeight", _screenHeight);
        _clusterBuildProgram.SetUniform("uGridDimX", GridDimensionX);
        _clusterBuildProgram.SetUniform("uGridDimY", GridDimensionY);
        _clusterBuildProgram.SetUniform("uGridDimZ", GridDimensionZ);
        _clusterBuildProgram.SetUniform("uZNear", camera.NearClipPlane);
        _clusterBuildProgram.SetUniform("uZFar", camera.FarClipPlane);

        _clusterBuildProgram.SetUniform("uInverseProjectionMatrix", camera.InverseProjectionMatrix);

        _clusterAABBBuffer.Bind(LightBindings.LightClusterAabbs);

        GL.DispatchCompute(_numWorkGroups, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        _clusterBuildProgram.Unuse();
    }

    private void CullLights(CameraComponent camera)
    {
        if (_numClusters == 0 || _lightDataBuffer.Count == 0)
        {
            return;
        }

        _lightCullingProgram.Use();
        _lightCullingProgram.SetUniform("uLightCount", _lightDataBuffer.Capacity);
        _lightCullingProgram.SetUniform("uGridDimX", GridDimensionX);
        _lightCullingProgram.SetUniform("uGridDimY", GridDimensionY);
        _lightCullingProgram.SetUniform("uGridDimZ", GridDimensionZ);
        _lightCullingProgram.SetUniform("uViewMatrix", camera.ViewMatrix);

        BindForRendering();
        _clusterAABBBuffer.Bind(LightBindings.LightClusterAabbs);

        GL.DispatchCompute(_numWorkGroups, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        _lightCullingProgram.Unuse();
    }

    protected override void OnComponentUpdate(LightComponent component, float delta)
    {
        var data = component.GetLightData();
        if (component._allocation is null)
        {
            component._allocation = _lightDataBuffer.Add(data);
        }
        else
        {
            _lightDataBuffer.Update(component._allocation.Value, data);
        }
    }

    protected override void OnActorComponentEnqueued(LightComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component is DirectionalLightComponent { CastShadows: true } dirLight)
        {
            DirectionalLight = dirLight;
        }
    }

    protected override void OnActorComponentRemoved(LightComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        // only a component leaving on its own gives its slot back: on a scene swap or a shutdown the
        // whole buffer goes with the system, so freeing slot by slot would be wasted work
        if (reason is EEndPlayReason.Destroyed && component._allocation is { } allocation)
        {
            _lightDataBuffer.Remove(allocation);
        }

        if (component == DirectionalLight)
        {
            DirectionalLight = null;
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        if (_screenWidth == newWidth && _screenHeight == newHeight) return;

        _screenWidth = newWidth;
        _screenHeight = newHeight;

        // Calculate grid dimensions based on 32-pixel tiles
        GridDimensionX = (_screenWidth + TileSize - 1) / TileSize;
        GridDimensionY = (_screenHeight + TileSize - 1) / TileSize;
        _numClusters = GridDimensionX * GridDimensionY * GridDimensionZ;
        _numWorkGroups = (_numClusters + WorkGroupSize - 1) / WorkGroupSize;

        _clusterAABBBuffer.Reallocate(_numClusters);
        _clusterDataBuffer.Reallocate(_numClusters);
        _lightIndexListBuffer.Reallocate(_numClusters * MaxLightsPerCluster);

        _clustersDirty = true; // grid changed, rebuild the cluster AABBs next frame
    }

    public long Allocated => _lightDataBuffer.Allocated;
    public long Used => _lightDataBuffer.Used;
    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Light Data Buffer", _lightDataBuffer);
    }

    internal void BindForRendering()
    {
        _lightDataBuffer.Bind(LightBindings.LightData);
        _clusterDataBuffer.Bind(LightBindings.LightClusterData);
        _lightIndexListBuffer.Bind(LightBindings.LightIndexList);
    }
    public override void Dispose()
    {
        base.Dispose();

        _clusterAABBBuffer.Dispose();
        _clusterDataBuffer.Dispose();
        _lightIndexListBuffer.Dispose();
        _clusterBuildProgram.Dispose();
        _lightCullingProgram.Dispose();
    }

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Lighting Table", () =>
        {
            ImGui.BeginDisabled(DirectionalLight == null);
            var check = DirectionalLight?.IsEnabled ?? false;
            if (EditorUI.Checkbox("Sun Light", ref check)) DirectionalLight?.IsEnabled = check;
            ImGui.EndDisabled();

            EditorUI.Text("Lights", $"{ComponentsCount}/{Capacity}");
            EditorUI.Text("Clusters", $"{_numClusters} ({GridDimensionX} x {GridDimensionY} x {GridDimensionZ}) split into {_numWorkGroups} work groups");
            EditorUI.Text("Buffer", $"{_lightDataBuffer.Count} Element(s) ({_lightDataBuffer.Used.GetReadableSize()} / {_lightDataBuffer.Allocated.GetReadableSize()})");
        });
    }
}
