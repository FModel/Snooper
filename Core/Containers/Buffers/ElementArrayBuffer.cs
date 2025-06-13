using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ElementArrayBuffer<T>(int capacity, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<T>(capacity, BufferTarget.ElementArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName Name => GetPName.ElementArrayBufferBinding;
}
