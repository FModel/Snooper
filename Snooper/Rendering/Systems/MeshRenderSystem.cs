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
        GL.VertexArrayAttribIFormat(vao, 0, 2, VertexAttribIType.UnsignedInt, 0);
        GL.VertexArrayAttribIFormat(vao, 1, 1, VertexAttribIType.UnsignedInt, 8);
        GL.VertexArrayAttribIFormat(vao, 2, 1, VertexAttribIType.UnsignedInt, 12);
        GL.VertexArrayAttribIFormat(vao, 3, 1, VertexAttribIType.UnsignedInt, 16);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.EnableVertexArrayAttrib(vao, 1);
        GL.EnableVertexArrayAttrib(vao, 2);
        GL.EnableVertexArrayAttrib(vao, 3);
        GL.VertexArrayAttribBinding(vao, 0, 0);
        GL.VertexArrayAttribBinding(vao, 1, 0);
        GL.VertexArrayAttribBinding(vao, 2, 0);
        GL.VertexArrayAttribBinding(vao, 3, 0);
    };

    protected override void OnLoad()
    {
        base.OnLoad();

        _shadowShader.Generate();
        _shadowShader.Link();
    }

    public void RenderShadows(IViewProjectionProvider[] cascades)
    {
        Resources.Cull(cascades[^1], CommandBufferType.Opaque, true); // use the farthest cascade camera for culling

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
