using System.Numerics;
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
    protected readonly ArrayBuffer<Vector3> VBO = new(0, BufferUsageHint.StaticDraw);
    protected readonly ElementArrayBuffer<uint> EBO = new(0, BufferUsageHint.StaticDraw);

    protected virtual PolygonMode PolygonMode { get; } = PolygonMode.Fill;

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

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VBO.Stride, 0);
        GL.EnableVertexAttribArray(0);
    }

    public virtual void Update()
    {

    }

    public virtual void Render()
    {
        VAO.Bind();
        VBO.Bind();
        EBO.Bind();

        var polygonMode = (PolygonMode)GL.GetInteger(GetPName.PolygonMode);
        var bDiff = polygonMode != PolygonMode;
        if (bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode);

        GL.DrawElements(PrimitiveType.Triangles, EBO.Size, DrawElementsType.UnsignedInt, 0);

        if (bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, polygonMode);
    }
}
