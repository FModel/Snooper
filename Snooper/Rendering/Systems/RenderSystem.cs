using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem() : PrimitiveSystem<Vertex, MeshComponent, PerInstanceData, PerMaterialMeshData>(500)
{
    public override uint Order => 22;
    protected override bool AllowDerivation => true;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("mesh");
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

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        
        shader.SetUniform("uDebugColorMode", (int)DebugColorMode);
    }

    public override bool Accepts(Type type) => type != typeof(SplineMeshComponent) && base.Accepts(type); // TODO: improve this

    protected override bool CanEnqueueActorComponent(MeshComponent component)
    {
        return component is { IsOpaque: false, IsVisible: true };
    }
}
