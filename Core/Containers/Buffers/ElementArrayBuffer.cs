using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ElementArrayBuffer<T>(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(size, BufferTarget.ElementArrayBuffer, usageHint) where T : unmanaged
{
    public ElementArrayBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }
}
