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

    public void UpdateInstance(int[] offsets, uint instanceCount, uint baseInstance)
    {
        var ptr = GL.MapBufferRange(BufferTarget.DrawIndirectBuffer, offsets[0] * Stride, offsets.Length * Stride, MapBufferAccessMask.MapReadBit | MapBufferAccessMask.MapWriteBit);

        unsafe
        {
            var commands = (DrawElementsIndirectCommand*)ptr;
            for (var i = 0; i < offsets.Length; i++)
            {
                commands[i].InstanceCount = instanceCount;
                commands[i].BaseInstance = baseInstance;
            }
        }

        GL.UnmapBuffer(BufferTarget.DrawIndirectBuffer);
    }

    public void UpdateFirstIndex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 8, 4, ref value);
    }

    public void UpdateBaseVertex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 12, 4, ref value);
    }

    public override void RemoveRange(int[] index)
    {
        UpdateInstance(index, 0, 0);
        base.RemoveRange(index);
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
