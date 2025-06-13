using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(int capacity, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<DrawElementsIndirectCommand>(capacity, BufferTarget.DrawIndirectBuffer, usageHint)
{
    public override GetPName Name => GetPName.DrawIndirectBufferBinding;

    public DrawElementsIndirectCommand this[int index] => GetData(index, 1)[0];

    public void UpdateCount(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride, 4, ref value);
    }

    public void UpdateInstanceCount(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 4, 4, ref value);
    }

    public void UpdateFirstIndex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 8, 4, ref value);
    }

    public void UpdateBaseVertex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 12, 4, ref value);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct DrawElementsIndirectCommand
{
    public uint Count;
    public uint InstanceCount;
    public uint FirstIndex;
    public uint BaseVertex;
    public uint BaseInstance;
}
