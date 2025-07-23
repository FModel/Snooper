namespace Snooper.Core.Containers.Buffers;

public class DoubleBuffer<TBuffer>(Func<TBuffer> factory) : IDisposable where TBuffer : HandledObject
{
    private readonly TBuffer[] _buffers = [factory(), factory()];
    private int _frameCount;

    public TBuffer Current => _buffers[_frameCount % 2];
    public TBuffer Next => _buffers[(_frameCount + 1) % 2];

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
}
