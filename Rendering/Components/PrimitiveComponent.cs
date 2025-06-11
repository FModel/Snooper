using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    protected readonly VertexArray VAO = new();
    protected readonly ArrayBuffer<T> VBO = new(0, BufferUsageHint.StaticDraw);
    protected readonly ElementArrayBuffer<uint> EBO = new(0, BufferUsageHint.StaticDraw);

    protected abstract Action<ArrayBuffer<T>> PointersFactory { get; }
    protected abstract PolygonMode PolygonMode { get; }

    private bool _bGenerated;

    public virtual void Generate()
    {
        VAO.Generate();
        VAO.Bind();

        VBO.Generate();
        VBO.Bind();
        VBO.SetData(primitive.Vertices);

        EBO.Generate();
        EBO.Bind();
        EBO.SetData(primitive.Indices);

        PointersFactory(VBO);
        _bGenerated = true;
    }

    public virtual void Update()
    {
        if (!_bGenerated)
        {
            Generate();
        }
    }

    public virtual void Render()
    {
        var polygonMode = (PolygonMode)GL.GetInteger(GetPName.PolygonMode);
        var bDiff = polygonMode != PolygonMode;
        if (bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode);

        VAO.Bind();
        GL.DrawElements(PrimitiveType.Triangles, EBO.Size, DrawElementsType.UnsignedInt, 0);

        if (bDiff) GL.PolygonMode(TriangleFace.FrontAndBack, polygonMode);
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive)
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
    protected override PolygonMode PolygonMode { get => PolygonMode.Fill; }
}
