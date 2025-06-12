using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class ShaderStorageBuffer<T>(int capacity, BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(capacity, BufferTarget.ShaderStorageBuffer, usageHint) where T : unmanaged
{
    public override GetPName Name => GetPName.ShaderStorageBufferBinding;

    public ShaderStorageBuffer(T[] data) : this(data.Length, BufferUsageHint.StaticDraw)
    {

    }

    public void Bind(int index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }
}
