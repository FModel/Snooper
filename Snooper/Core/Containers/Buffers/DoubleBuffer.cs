namespace Snooper.Core.Containers.Buffers;

public class DoubleBuffer<TBuffer>(Func<TBuffer> factory) : IMemoryDetailsProvider, IDisposable where TBuffer : HandledObject
{
    private readonly TBuffer[] _buffers = [factory(), factory()];
    private int _frameCount;

    public TBuffer Previous => _buffers[_frameCount % 2];
    public TBuffer Current => _buffers[(_frameCount + 1) % 2];

    public void Swap() => _frameCount++;

    public void Generate()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Generate();
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += Previous.Allocated;
            total += Current.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += Previous.Used;
            total += Current.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Previous Buffer", Previous);
        yield return new MemoryDetail("Current Buffer", Current);
    }
}
