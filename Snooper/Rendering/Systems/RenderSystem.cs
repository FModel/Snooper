using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class RenderSystem() : PrimitiveSystem<Vertex, MeshComponent, PerInstanceData>(500)
{
    public override uint Order => 22;
    protected override bool AllowDerivation => true;
    protected override int BatchCount => int.MaxValue;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("mesh");
    protected override Action<ArrayBuffer<Vertex>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, buffer.Stride, 12);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, buffer.Stride, 24);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, buffer.Stride, 36);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);
        GL.EnableVertexAttribArray(3);
    };

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
        
        Shader.SetUniform("uDebugColorMode", (int)DebugColorMode);
    }
}
