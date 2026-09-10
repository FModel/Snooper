using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;

namespace Snooper.Core.Containers.Buffers;

public sealed class ShaderStorageBuffer<T>(BufferUsageHint usageHint = BufferUsageHint.StaticDraw, int slices = 1) : Buffer<T>(BufferTarget.ShaderStorageBuffer, usageHint, slices), IIndexedBind where T : unmanaged
{
    public override GetPName PName => GetPName.ShaderStorageBufferBinding;

    private readonly BufferUpdateBatcher<T> _batcher = new();

    public void Bind(uint index)
    {
        // iGPUs don't like binding unallocated buffers, and unlike some other buffers, we never unbind SSBOs
        // silently skipping the bind leaves whatever another system bound at this index in place
        // that's too risky, so we unbind the slot instead so an unallocated buffer reads as out of range
        // TODO: we should properly unbind SSBOs, and skip if unallocated
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, IsAllocated ? Handle : 0);
    }

    public void QueueUpdate(BufferAllocation allocation, T data) => _batcher.Add(allocation, data);
    public void QueueUpdate(BufferAllocation allocation, T[] data) => _batcher.Add(allocation, data);

    public void FlushUpdates() => _batcher.Flush(this);

    public int PendingUpdateCount => _batcher.Count;
}
