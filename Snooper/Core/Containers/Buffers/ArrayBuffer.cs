using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ArrayBuffer<T>(BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<T>(BufferTarget.ArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName PName => GetPName.ArrayBufferBinding;
}
