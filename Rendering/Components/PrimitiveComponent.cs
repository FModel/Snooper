using System.Numerics;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components;

public abstract class TPrimitiveComponent<T>(TPrimitiveData<T> primitive) : ActorComponent where T : unmanaged
{
    private DrawElementsIndirectCommand? _drawCommand;

    public void Generate(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        _drawCommand = new DrawElementsIndirectCommand
        {
            Count = (uint) primitive.Indices.Length,
            InstanceCount = 1,
            FirstIndex = (uint) ebo.Size,
            BaseVertex = (uint) vbo.Size,
            BaseInstance = 0
        };

        DrawId = commands.Size;

        commands.Add(_drawCommand.Value);
        ebo.AddRange(primitive.Indices);
        vbo.AddRange(primitive.Vertices);
    }

    public virtual void Update(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<T> vbo)
    {
        if (_drawCommand == null)
        {
            Generate(commands, ebo, vbo);
        }
    }
}

[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(IPrimitiveData primitive) : TPrimitiveComponent<Vector3>(primitive);
