using System.Diagnostics;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class CullingResources : IMemoryDetailsProvider, IDisposable
{
    private readonly ShaderStorageBuffer<PerMeshData> _meshes = new();
    private readonly ShaderStorageBuffer<PrimitiveOffsets> _primitives = new();
    private readonly ShaderStorageBuffer<SectionOffsets> _sections = new();
    private readonly ComputeShader _compute = new("culling.comp")
    {
        Defines = [$"MAX_CULLING_VIEWS {Settings.MaxCullingViews}", ..CullingBindings.OwnDefines]
    };

    private abstract class CullingBindings : Bindings
    {
        public const uint DrawCommands = BaseMaxBinding + 1;
        public const uint CullLodData = BaseMaxBinding + 2;
        public const uint CullSections = BaseMaxBinding + 3;
        public const uint MaxBinding = CullSections;

        public static readonly string[] OwnDefines =
        [
            Define("DRAW_COMMANDS", DrawCommands),
            Define("CULL_LOD_DATA", CullLodData),
            Define("CULL_SECTIONS", CullSections)
        ];
    }

    public void Generate()
    {
        _meshes.Generate();
        _primitives.Generate();
        _sections.Generate();

        _compute.Generate();
        _compute.Link();
    }

    public void Allocate(AllocationCounts counts)
    {
        if (counts.UniqueComponents > 0)
        {
            _meshes.Allocate(counts.UniqueComponents);
            _primitives.Allocate(counts.UniqueComponents);
        }
        if (counts.Sections > 0) _sections.Allocate(counts.Sections);
    }

    public BufferAllocation Add(SectionDescriptor[] sections)
    {
        var offsets = new SectionOffsets[sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            offsets[i] = new SectionOffsets(sections[i]);
        }

        return _sections.AddRange(offsets);
    }

    public BufferAllocation Add(PerMeshData mesh, PrimitiveOffsets lods)
    {
        var meshAllocation = _meshes.Add(mesh);
        var lodAllocation = _primitives.Add(lods);
        Debug.Assert(meshAllocation.StartIndex == lodAllocation.StartIndex, "PerMeshData and PrimitiveOffsets buffers must stay index-aligned.");
        return meshAllocation;
    }

    public void UpdateOverrideLod(BufferAllocation allocation, int overrideLod)
    {
        _meshes.UpdateCustom(allocation, overrideLod, PerMeshData.OverrideLodOffset);
    }

    public void BindMeshData() => _meshes.Bind(Bindings.MeshData);

    private readonly Plane[] _planes = new Plane[Settings.MaxCullingViews * 6];
    private readonly Vector4[] _lodReferences = new Vector4[Settings.MaxCullingViews];
    private readonly float[] _lodOrthoExtents = new float[Settings.MaxCullingViews];

    public void Cull<TInstanceData>(ReadOnlySpan<CullView> views, ShaderStorageBuffer<TInstanceData> instances, IndirectDrawBuffer commands) where TInstanceData : unmanaged, IPerInstanceData
    {
        var viewCount = Math.Min(views.Length, commands.ViewCount);
        if (viewCount <= 0 || commands.Capacity == 0) return;

        for (var i = 0; i < viewCount; i++)
        {
            var matrix = views[i].ViewProjection;
            var b = i * 6;
            _planes[b + 0] = new Plane(matrix.M14 + matrix.M11, matrix.M24 + matrix.M21, matrix.M34 + matrix.M31, matrix.M44 + matrix.M41); // Near
            _planes[b + 1] = new Plane(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21, matrix.M34 - matrix.M31, matrix.M44 - matrix.M41); // Far
            _planes[b + 2] = new Plane(matrix.M14 + matrix.M12, matrix.M24 + matrix.M22, matrix.M34 + matrix.M32, matrix.M44 + matrix.M42); // Left
            _planes[b + 3] = new Plane(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22, matrix.M34 - matrix.M32, matrix.M44 - matrix.M42); // Right
            _planes[b + 4] = new Plane(matrix.M14 + matrix.M13, matrix.M24 + matrix.M23, matrix.M34 + matrix.M33, matrix.M44 + matrix.M43); // Bottom
            _planes[b + 5] = new Plane(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23, matrix.M34 - matrix.M33, matrix.M44 - matrix.M43); // Top

            _lodReferences[i] = new Vector4(views[i].LodReferencePosition, views[i].LodProjectionScale);
            _lodOrthoExtents[i] = views[i].LodOrthoExtent;
        }

        _compute.Use();
        _compute.SetUniform("uFrustumPlanes", _planes);
        _compute.SetUniform("uLodReference", _lodReferences);
        _compute.SetUniform("uLodOrthoExtent", _lodOrthoExtents);
        _compute.SetUniform("uViewCount", (uint) viewCount);
        _compute.SetUniform("uViewCapacity", (uint) commands.Capacity);

        commands.Commands.Bind(CullingBindings.DrawCommands);
        commands.StaticData.Bind(Bindings.DrawStatic);
        commands.CulledData.Bind(Bindings.DrawCulled);
        instances.Bind(Bindings.InstanceData);
        BindMeshData();
        _primitives.Bind(CullingBindings.CullLodData);
        _sections.Bind(CullingBindings.CullSections);

        GL.DispatchCompute(commands.Capacity, viewCount, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit | MemoryBarrierFlags.ShaderStorageBarrierBit);
        _compute.Unuse();
    }

    public void Remove(int index)
    {
        // _primitives.Bind();
        // _primitives.Remove();
        // _primitives.Unbind();
        //
        // _sections.Bind();
        // _sections.Remove();
        // _sections.Unbind();
    }

    public void Dispose()
    {
        _meshes.Dispose();
        _primitives.Dispose();
        _sections.Dispose();
        _compute.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _meshes.Allocated;
            total += _primitives.Allocated;
            total += _sections.Allocated;
            total += _compute.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _meshes.Used;
            total += _primitives.Used;
            total += _sections.Used;
            total += _compute.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Mesh Data", _meshes);
        yield return new MemoryDetail("Primitive Offsets", _primitives);
        yield return new MemoryDetail("Section Offsets", _sections);
        yield return new MemoryDetail("Culling Compute Shader", _compute);
    }
}
