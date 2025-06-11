using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ShaderStorageBuffer<T>(int size, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(size, BufferTarget.ShaderStorageBuffer, usageHint) where T : unmanaged
{
    public ShaderStorageBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }

    public void Bind(int index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }
}
