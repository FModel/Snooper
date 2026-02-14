using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public class BillboardSystem : PrimitiveSystem<Vector2, BillboardComponent, PerInstanceData, PerMaterialBillboardData>
{
    public override uint Order => 29;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("billboard")
    };

    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 2, VertexAttribType.Float, false, 0);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.VertexArrayAttribBinding(vao, 0, 0);
    };
}
