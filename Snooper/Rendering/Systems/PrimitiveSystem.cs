using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type = PrimitiveType.Triangles, int viewCount = 1)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(type, viewCount)
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected override bool AllowDerivation => false;
    protected virtual bool IsCulled => true; // whether the bounds of this system's components can be trusted
    protected virtual Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("default")
    };

    private ShaderProgram? _maskShader;

    protected override void OnLoad()
    {
        base.OnLoad();

        if (!Shaders.TryGetValue(CommandBufferType.Transparent, out var mainShader) &&
            !Shaders.TryGetValue(CommandBufferType.Opaque, out mainShader))
        {
            throw new InvalidOperationException("At least one shader (opaque or transparent) must be provided.");
        }

        _maskShader = (ShaderProgram) mainShader.Clone();
        _maskShader.Fragment = "empty.frag";

        foreach (var shader in Shaders.Values.Append(_maskShader))
        {
            shader.Generate();
            shader.Link();
        }
    }

    protected virtual void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        shader.Use();
        shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
        shader.SetUniform("uFragmentColorMode", ActorManager?.FragmentColor ?? FragmentColorMode.Disabled);
        var wireframe = ActorManager?.Wireframe;
        var wired = wireframe is not null && (wireframe.Enabled || ShowWireframe);
        shader.SetUniform("uWireframe", wired);
        if (wired)
        {
            shader.SetUniform("uWireframeColor", wireframe!.Color);
            shader.SetUniform("uWireframeWidth", wireframe.Width);
            shader.SetUniform("uWireframeOverlay", wireframe.Overlay);
        }
        shader.SetUniform("uViewBase", 0u); // the main camera is always view 0
    }

    public override void Cull(ReadOnlySpan<CullView> views)
    {
        if (!IsEnabled) return;

        using (Scope())
        using (Profiler.Cull())
        {
            foreach (var type in Shaders.Keys)
            {
                // opaque draws are culled for every view (main camera + shadow cameras)
                // transparent draws are culled for the camera only, they cast no shadow
                // a system whose bounds cannot be trusted culls nothing, it only gets its mask slice
                if (!IsCulled) Resources.BuildMask(type);
                else Resources.Cull(type == CommandBufferType.Opaque ? views : views[..1], type);
            }
        }
    }

    protected sealed override void OnRender(CameraComponent camera, CommandBufferType type)
    {
        if (!Shaders.TryGetValue(type, out var shader))
        {
            // Log.Warning("No shader found for command buffer type {Type} in {System}.", type, DisplayName);
            return;
        }

        using (Profiler.Draw())
        {
            PreRender(camera, shader);
            BindSystemBuffers();
            base.OnRender(camera, type);
            PostRender(camera, shader);
        }
    }

    public override void RenderMask(CameraComponent camera)
    {
        if (!IsEnabled || _maskShader is null) return;

        using (Scope())
        using (Profiler.Draw())
        {
            PreRender(camera, _maskShader);
            BindSystemBuffers();
            foreach (var type in Shaders.Keys)
            {
                _maskShader.SetUniform("uViewBase", Resources.GetMaskViewBase(type));
                Resources.RenderMask(type);
            }
            PostRender(camera, _maskShader);
        }
    }

    protected virtual void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        shader.Unuse();
    }

    public override long Allocated
    {
        get
        {
            long total = base.Allocated + (_maskShader?.Allocated ?? 0);
            foreach (var shader in Shaders.Values)
                total += shader.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = base.Used + (_maskShader?.Used ?? 0);
            foreach (var shader in Shaders.Values)
                total += shader.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        foreach (var (type, shader) in Shaders)
            yield return new MemoryDetail($"{type} Shader", shader);

        if (_maskShader is not null)
            yield return new MemoryDetail("Mask Shader", _maskShader);
    }

    public override void Dispose()
    {
        base.Dispose();

        foreach (var shader in Shaders.Values)
            shader.Dispose();
        _maskShader?.Dispose();
    }
}

public class PrimitiveSystem<TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerMaterialData>(type)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint Order => 20;

    protected override Action<VertexArrayLayout> VertexLayout { get; } = layout => layout.Float(0, 3);
}

public class PrimitiveSystem<TComponent>(PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<TComponent, PerInstanceData, PerMaterialData>(type)
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerMaterialData>
{
    protected override bool IsCulled => false; // disable culling for grid, skybox, and default primitives
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
