using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    public int DrawId { get; private set; } = -1;

    public void Generate(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        DrawId = commands.Add(new DrawElementsIndirectCommand
        {
            Count = (uint) primitive.Indices.Length,
            InstanceCount = 1,
            FirstIndex = (uint) ebo.Count,
            BaseVertex = (uint) vbo.Count,
            BaseInstance = 0
        });
        ebo.AddRange(primitive.Indices);
        vbo.AddRange(primitive.Vertices);
    }

    public virtual void Update(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        if (DrawId < 0)
        {
            Generate(commands, ebo, vbo);
        }
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive);
