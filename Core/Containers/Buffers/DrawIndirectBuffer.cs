using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(int capacity, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<DrawElementsIndirectCommand>(capacity, BufferTarget.DrawIndirectBuffer, usageHint)
{
    public override GetPName Name => GetPName.DrawIndirectBufferBinding;

    public DrawIndirectBuffer(DrawElementsIndirectCommand[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }

    public void UpdateInstanceCount(int offset, uint value)
    {
        GL.BufferSubData(Target, offset * Stride + sizeof(uint), sizeof(uint), ref value);
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
