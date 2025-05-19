using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : ActorComponent
{
    public readonly IPrimitiveData Primitive = primitive;

    private readonly VertexArray _vertexArray = new();
    private readonly ArrayBuffer<float> _vertexBuffer = new(0, BufferUsageHint.StaticDraw);
    private readonly ElementArrayBuffer<ushort> _indexBuffer = new(0, BufferUsageHint.StaticDraw);

    public void Generate()
    {
        _vertexArray.Generate();
        _vertexArray.Bind();

        _vertexBuffer.Generate();
        _vertexBuffer.Bind();
        _vertexBuffer.SetData(Primitive.Vertices);

        _indexBuffer.Generate();
        _indexBuffer.Bind();
        _indexBuffer.SetData(Primitive.Indices);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * _vertexBuffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    }

    public void Render()
    {
        _vertexArray.Bind();
        GL.DrawElements(PrimitiveType.Triangles, _indexBuffer.Size, DrawElementsType.UnsignedShort, 0);
    }
}
