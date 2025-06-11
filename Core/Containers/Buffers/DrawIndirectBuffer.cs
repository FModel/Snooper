using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class DrawIndirectBuffer<T>(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(size, BufferTarget.DrawIndirectBuffer, usageHint) where T : unmanaged
{
    public DrawIndirectBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }
}
