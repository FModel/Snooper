using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public class BillboardSystem() : PrimitiveSystem<Vector2, BillboardComponent, PerInstanceData, PerDrawBillboardData>(100)
{
    public override uint Order => 29;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("billboard");
    protected override Action<int> VertexLayout { get; } = stride =>
    {
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}