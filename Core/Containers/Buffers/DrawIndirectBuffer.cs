using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<DrawElementsIndirectCommand>(size, BufferTarget.DrawIndirectBuffer, usageHint)
{
    public DrawIndirectBuffer(DrawElementsIndirectCommand[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

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
