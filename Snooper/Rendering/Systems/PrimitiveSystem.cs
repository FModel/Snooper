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
    protected virtual bool IsCulled => true;
    protected virtual Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("default")
    };

    protected override void OnLoad()
    {
        base.OnLoad();

        if (Shaders.ContainsKey(CommandBufferType.Mask))
            throw new InvalidOperationException("Mask shader is auto generated and cannot be set manually.");

        if (!Shaders.TryGetValue(CommandBufferType.Transparent, out var mainShader) &&
            !Shaders.TryGetValue(CommandBufferType.Opaque, out mainShader))
        {
            throw new InvalidOperationException("At least one shader (opaque or transparent) must be provided.");
        }

        var maskShader = (ShaderProgram) mainShader.Clone();
        maskShader.Fragment = "empty.frag";
        Shaders.Add(CommandBufferType.Mask, maskShader);

        foreach (var shader in Shaders.Values)
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
        shader.SetUniform("uViewBase", 0u); // the main camera is always view 0
    }

    public override void Cull(ReadOnlySpan<CullView> views)
    {
        if (!IsEnabled || !IsCulled) return;

        using (Scope())
        using (Profiler.Cull())
        {
            foreach (var type in Shaders.Keys)
            {
                Resources.Cull(type == CommandBufferType.Opaque ? views : views[..1], type);
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

    protected virtual void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        shader.Unuse();
    }

    public override long Allocated
    {
        get
        {
            long total = base.Allocated;
            foreach (var shader in Shaders.Values)
                total += shader.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = base.Used;
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
    }

    public override void Dispose()
    {
        base.Dispose();

        foreach (var shader in Shaders.Values)
            shader.Dispose();
    }
}

public class PrimitiveSystem<TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerMaterialData>(type)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint Order => 20;

    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 3, VertexAttribType.Float, false, 0);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.VertexArrayAttribBinding(vao, 0, 0);
    };
}

public class PrimitiveSystem<TComponent>(PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<TComponent, PerInstanceData, PerMaterialData>(type)
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerMaterialData>
{
    protected override bool IsCulled => false; // disable culling for grid, skybox, and default primitives
}

public class PrimitiveSystem : PrimitiveSystem<PrimitiveComponent>;
