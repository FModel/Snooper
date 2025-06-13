using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ArrayBuffer<T>(int capacity, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<T>(capacity, BufferTarget.ArrayBuffer, usageHint) where T : unmanaged
{
    public override GetPName Name => GetPName.ArrayBufferBinding;
}
