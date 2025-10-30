using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ElementArrayBuffer<T>(BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<T>(BufferTarget.ElementArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName PName => GetPName.ElementArrayBufferBinding;
}
