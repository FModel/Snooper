using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(int capacity, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<DrawElementsIndirectCommand>(capacity, BufferTarget.DrawIndirectBuffer, usageHint)
{
    public override GetPName Name => GetPName.DrawIndirectBufferBinding;
    
    public void Bind(int index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }

    public DrawElementsIndirectCommand this[int index] => GetData(index, 1)[0];

    public void UpdateIndexCount(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride, 4, ref value);
    }

    public void UpdateInstance(int offset, uint instanceCount, uint baseInstance)
    {
        GL.BufferSubData(Target, offset * Stride + 4, 4, ref instanceCount);
        GL.BufferSubData(Target, offset * Stride + 16, 4, ref baseInstance);
    }

    public void UpdateFirstIndex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 8, 4, ref value);
    }

    public void UpdateBaseVertex(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + 12, 4, ref value);
    }

    public override void RemoveRange(int[] indices)
    {
        foreach (var index in indices)
            UpdateInstance(index, 0, 0);
        
        base.RemoveRange(indices);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct DrawElementsIndirectCommand
{
    public uint IndexCount;
    public uint InstanceCount;
    public uint FirstIndex;
    public uint BaseVertex;
    public uint BaseInstance;
}
