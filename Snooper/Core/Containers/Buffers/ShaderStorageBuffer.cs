using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;

namespace Snooper.Core.Containers.Buffers;

public sealed class ShaderStorageBuffer<T>(BufferUsageHint usageHint = BufferUsageHint.StaticDraw, int slices = 1) : Buffer<T>(BufferTarget.ShaderStorageBuffer, usageHint, slices), IIndexedBind where T : unmanaged
{
    public override GetPName PName => GetPName.ShaderStorageBufferBinding;

    private readonly BufferUpdateBatcher<T> _batcher = new();

    public void Bind(uint index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }

    public void QueueUpdate(BufferAllocation allocation, T data) => _batcher.Add(allocation, data);
    public void QueueUpdate(BufferAllocation allocation, T[] data) => _batcher.Add(allocation, data);

    public void FlushUpdates() => _batcher.Flush(this);

    public int PendingUpdateCount => _batcher.Count;
}
