using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Buffers;

public sealed class UniformBuffer<T>(BufferUsageHint usageHint = BufferUsageHint.DynamicDraw) : Buffer<T>(BufferTarget.UniformBuffer, usageHint), IIndexedBind where T : unmanaged
{
    public override GetPName PName => GetPName.UniformBufferBinding;

    public void Bind(uint index)
    {
        // same as <see cref="ShaderStorageBuffer{T}.Bind(uint)"/>
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, index, IsAllocated ? Handle : 0);
    }
}
