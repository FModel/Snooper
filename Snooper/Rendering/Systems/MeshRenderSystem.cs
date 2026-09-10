using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public abstract class MeshRenderSystem<TComponent>(string[]? defines = null, int viewCount = Settings.MaxCullingViews)
    : PrimitiveSystem<Vertex, TComponent, PerInstanceData, PerMaterialMeshData>(PrimitiveType.Triangles, viewCount), IMeshRenderSystem where TComponent : MeshComponent
{
    protected override bool AllowDerivation => true;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("mesh") { Defines = defines },
        [CommandBufferType.Opaque] = new EmbeddedShader("mesh.vert", "geometry.frag") { Defines = defines }
    };

    private readonly ShaderProgram _shadowShader = new EmbeddedShader("Shadows/shadow_cascade.vert", "empty.frag")
    {
        Defines = defines
    };

    // see MeshComponent packed vertex layout
    protected override Action<VertexArrayLayout> VertexLayout { get; } = layout => layout
        .Integer(0, 2)
        .Float(1, 4, VertexAttribType.Int2101010Rev, normalized: true, offset: 8)
        .Integer(2, 1, offset: 12)
        .Integer(3, 1, offset: 16);

    protected override void OnLoad()
    {
        base.OnLoad();

        _shadowShader.Generate();
        _shadowShader.Link();
    }

    public void RenderShadowCascade(ShadowMapView cascade)
    {
        using (Scope())
        {
            const CommandBufferType buffer = CommandBufferType.Opaque;
            var view = cascade.ViewIndex;

            _shadowShader.Use();
            _shadowShader.SetUniform("uViewProjection", cascade.ViewProjection);
            _shadowShader.SetUniform("uViewBase", Resources.GetViewBase(buffer, view));

            using (Profiler.Draw())
            {
                BindSystemBuffers();
                Resources.Render(buffer, view);
            }

            _shadowShader.Unuse();
        }
    }

    public IEnumerable<MeshComponent> GetMeshComponents() => GetComponents<TComponent>();

    public override long Allocated => base.Allocated + _shadowShader.Allocated;
    public override long Used => base.Used + _shadowShader.Used;

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Shadow Shader", _shadowShader);
    }

    public override void Dispose()
    {
        base.Dispose();
        _shadowShader.Dispose();
    }
}
