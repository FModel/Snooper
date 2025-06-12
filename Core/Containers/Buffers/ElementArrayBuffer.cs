using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ElementArrayBuffer<T>(int capacity, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(capacity, BufferTarget.ElementArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName Name => GetPName.ElementArrayBufferBinding;

    public ElementArrayBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }
}
