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
    protected readonly VertexArray VAO = new();
    protected readonly ArrayBuffer<float> VBO = new(0, BufferUsageHint.StaticDraw);
    protected readonly ElementArrayBuffer<uint> EBO = new(0, BufferUsageHint.StaticDraw);

    public void Generate()
    {
        VAO.Generate();
        VAO.Bind();

        VBO.Generate();
        VBO.Bind();
        VBO.SetData(primitive.Vertices);

        EBO.Generate();
        EBO.Bind();
        EBO.SetData(primitive.Indices);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * VBO.Stride, 0);
        GL.EnableVertexAttribArray(0);
    }

    public void Render()
    {
        VAO.Bind();
        VBO.Bind();
        EBO.Bind();

        GL.DrawElements(PrimitiveType.Triangles, EBO.Size, DrawElementsType.UnsignedInt, 0);
    }
}
