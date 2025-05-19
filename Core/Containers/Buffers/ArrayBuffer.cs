using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ArrayBuffer<T>(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(size, BufferTarget.ArrayBuffer, usageHint) where T : unmanaged
{
    public ArrayBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }
}
