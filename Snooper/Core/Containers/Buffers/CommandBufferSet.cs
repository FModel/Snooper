namespace Snooper.Core.Containers.Buffers;

public enum CommandBufferType
{
    Opaque,
    Transparent
}

public class CommandBufferSet(int viewCount = 1) : IMemoryDetailsProvider, IDisposable
{
    // + 1 view for the outlined draws
    private readonly IndirectDrawBuffer _opaque = new(viewCount + 1);
    private readonly IndirectDrawBuffer _transparent = new(2);

    public void Generate()
    {
        _opaque.Generate();
        _transparent.Generate();
    }

    public void Allocate(uint totalDraws)
    {
        _opaque.Allocate((uint)Math.Ceiling(totalDraws * 0.7));
        _transparent.Allocate((uint)Math.Ceiling(totalDraws * 0.25));
    }

    public IndirectDrawBuffer GetBuffer(CommandBufferType type) => type switch
    {
        CommandBufferType.Opaque => _opaque,
        CommandBufferType.Transparent => _transparent,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public DrawAllocation Transfer(DrawAllocation allocation, CommandBufferType from, CommandBufferType to)
    {
        if (from == to) return allocation;

        var target = GetBuffer(to).CopyFrom(GetBuffer(from), allocation);
        GetBuffer(from).Remove(allocation);
        return target;
    }

    public void Dispose()
    {
        _opaque.Dispose();
        _transparent.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _opaque.Allocated;
            total += _transparent.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _opaque.Used;
            total += _transparent.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Opaque Commands", _opaque);
        yield return new MemoryDetail("Transparent Commands", _transparent);
    }
}
