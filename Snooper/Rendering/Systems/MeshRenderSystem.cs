using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

[DefaultActorSystem(typeof(MeshRenderSystem))]
public class MeshRenderSystem : PrimitiveSystem<Vertex, MeshComponent, PerInstanceData, PerMaterialMeshData>, IShadowSystem
{
    public override uint Order => 22;
    protected override bool AllowDerivation => true;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("mesh"),
        [CommandBufferType.Opaque] = new EmbeddedShader("mesh.vert", "geometry.frag")
    };

    private readonly ShaderProgram _shadowShader = new EmbeddedShader("Shadows/shadow_cascade.vert", "empty.frag")
    {
        Geometry = "Shadows/shadow_cascade.geom"
    };

    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 3, VertexAttribType.Float, false, 0);
        GL.VertexArrayAttribFormat(vao, 1, 3, VertexAttribType.Float, false, 12);
        GL.VertexArrayAttribFormat(vao, 2, 3, VertexAttribType.Float, false, 24);
        GL.VertexArrayAttribFormat(vao, 3, 2, VertexAttribType.Float, false, 36);
        GL.VertexArrayAttribIFormat(vao, 4, 1, VertexAttribType.UnsignedInt, 44);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.EnableVertexArrayAttrib(vao, 1);
        GL.EnableVertexArrayAttrib(vao, 2);
        GL.EnableVertexArrayAttrib(vao, 3);
        GL.EnableVertexArrayAttrib(vao, 4);
        GL.VertexArrayAttribBinding(vao, 0, 0);
        GL.VertexArrayAttribBinding(vao, 1, 0);
        GL.VertexArrayAttribBinding(vao, 2, 0);
        GL.VertexArrayAttribBinding(vao, 3, 0);
        GL.VertexArrayAttribBinding(vao, 4, 0);
    };

    protected override void OnLoad()
    {
        base.OnLoad();

        _shadowShader.Generate();
        _shadowShader.Link();
    }

    public void RenderShadows(IViewProjectionProvider[] cascades)
    {
        Resources.Cull(cascades[^1], CommandBufferType.Opaque); // use the farthest cascade camera for culling

        _shadowShader.Use();
        for (int i = 0; i < cascades.Length; i++)
        {
            _shadowShader.SetUniform($"uViewMatrices[{i}]", cascades[i].ViewMatrix);
            _shadowShader.SetUniform($"uProjectionMatrices[{i}]", cascades[i].ProjectionMatrix);
        }

        Resources.Render(CommandBufferType.Opaque); // Only render opaque meshes for shadows
    }

    public override bool Accepts(Type type) => type != typeof(SplineMeshComponent) && base.Accepts(type);

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
