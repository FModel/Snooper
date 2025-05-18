using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public class ElementArrayBuffer<T>(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(size, BufferTarget.ElementArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName PName { get => GetPName.ElementArrayBufferBinding; }

    public ElementArrayBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }
}
