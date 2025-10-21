using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;

namespace Snooper.Core.Containers.Buffers;

public sealed class ShaderStorageBuffer<T>(int capacity, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : Buffer<T>(capacity, BufferTarget.ShaderStorageBuffer, usageHint) where T : unmanaged
{
    public override GetPName PName => GetPName.ShaderStorageBufferBinding;
    
    private readonly BufferUpdateBatcher<T> _batcher = new();

    public void Bind(int index)
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);
    }
    
    public void QueueUpdate(int offset, T data) => _batcher.Add(offset, data);
    public void QueueUpdate(int offset, T[] data) => _batcher.Add(offset, data);
    
    public void FlushUpdates() => _batcher.Flush(this);
    
    public int PendingUpdateCount => _batcher.Count;
}
