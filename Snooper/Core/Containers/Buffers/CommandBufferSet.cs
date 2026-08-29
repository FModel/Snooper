namespace Snooper.Core.Containers.Buffers;

public enum CommandBufferType
{
    Opaque,
    Transparent,
    Mask
}

public class CommandBufferSet(int viewCount = 1) : IMemoryDetailsProvider, IDisposable
{
    private readonly IndirectDrawBuffer _opaque = new(viewCount);
    private readonly IndirectDrawBuffer _transparent = new();
    private readonly IndirectDrawBuffer _mask = new();

    private IndirectDrawBuffer.DeferMergeScope? _opaqueScope;
    private IndirectDrawBuffer.DeferMergeScope? _transparentScope;

    public void Generate()
    {
        _opaque.Generate();
        _transparent.Generate();
        _mask.Generate();
    }

    public void Allocate(uint totalDraws)
    {
        _opaque.Allocate((uint)Math.Ceiling(totalDraws * 0.7));
        _transparent.Allocate((uint)Math.Ceiling(totalDraws * 0.25));
        _mask.Allocate(totalDraws);
    }

    public IndirectDrawBuffer GetBuffer(CommandBufferType type) => type switch
    {
        CommandBufferType.Opaque => _opaque,
        CommandBufferType.Transparent => _transparent,
        CommandBufferType.Mask => _mask,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public void BeginDeferMerge()
    {
        _opaqueScope = _opaque.DeferMerge();
        _transparentScope = _transparent.DeferMerge();
    }

    public void EndDeferMerge()
    {
        _opaqueScope?.Dispose();
        _opaqueScope = null;
        _transparentScope?.Dispose();
        _transparentScope = null;
    }

    public DrawAllocation Transfer(DrawAllocation sourceAllocation, CommandBufferType from, CommandBufferType to)
    {
        if (from == to) return sourceAllocation;

        var sourceBuffer = GetBuffer(from);
        var targetBuffer = GetBuffer(to);
        var delete = (from == CommandBufferType.Opaque && to == CommandBufferType.Transparent) ||
                     (from == CommandBufferType.Transparent && to == CommandBufferType.Opaque);

        var targetAllocation = targetBuffer.CopyFrom(sourceBuffer, sourceAllocation);

        // only remove from source if transferring between opaque/transparent (not copying to mask)
        if (delete)
        {
            sourceBuffer.Remove(sourceAllocation);
        }

        return targetAllocation;
    }

    public void ClearMask()
    {
        _mask.Clear();
    }

    public void Dispose()
    {
        _opaque.Dispose();
        _transparent.Dispose();
        _mask.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _opaque.Allocated;
            total += _transparent.Allocated;
            total += _mask.Allocated;
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
            total += _mask.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Opaque Commands", _opaque);
        yield return new MemoryDetail("Transparent Commands", _transparent);
        yield return new MemoryDetail("Mask Commands", _mask);
    }
}
