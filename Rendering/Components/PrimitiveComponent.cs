using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    protected DrawElementsIndirectCommand? DrawCommand;

    protected abstract PolygonMode PolygonMode { get; }

    public void Generate(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        DrawCommand = new DrawElementsIndirectCommand
        {
            Count = (uint) primitive.Indices.Length,
            InstanceCount = 1,
            FirstIndex = (uint) ebo.Size,
            BaseVertex = (uint) vbo.Size,
            BaseInstance = (uint) commands.Size
        };

        commands.Add(DrawCommand.Value);
        ebo.AddRange(primitive.Indices);
        vbo.AddRange(primitive.Vertices);
    }

    public virtual void Update(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        if (DrawCommand == null)
        {
            Generate(commands, ebo, vbo);
        }
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive)
{
    protected override PolygonMode PolygonMode { get => PolygonMode.Fill; }
}
