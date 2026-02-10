using Serilog;

namespace Snooper.Core.Containers.Buffers;

public enum CommandBufferType
{
    Opaque,
    Transparent,
    Mask
}

public class CommandBufferSet : IMemoryDetailsProvider, IDisposable
{
    private readonly DrawIndirectBuffer _opaque = new();
    private readonly DrawIndirectBuffer _transparent = new();
    private readonly DrawIndirectBuffer _mask = new();

    private readonly Queue<(BufferAllocation source, CommandBufferType from, CommandBufferType to)> _pendingTransfers = new();

    public void Generate()
    {
        _opaque.Generate();
        _transparent.Generate();
        _mask.Generate();
    }

    public void Allocate(uint totalDraws)
    {
        // 70% opaque, 25% transparent, 100% mask
        _opaque.Allocate((uint)Math.Ceiling(totalDraws * 0.7));
        _transparent.Allocate((uint)Math.Ceiling(totalDraws * 0.25));
        _mask.Allocate(totalDraws);

        Log.Debug("Allocated CommandBufferSet: {OpaqueCapacity} opaque, {TransparentCapacity} transparent, {MaskCapacity} mask", _opaque.Capacity, _transparent.Capacity, _mask.Capacity);
    }

    public DrawIndirectBuffer GetBuffer(CommandBufferType type) => type switch
    {
        CommandBufferType.Opaque => _opaque,
        CommandBufferType.Transparent => _transparent,
        CommandBufferType.Mask => _mask,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public void QueueTransfer(BufferAllocation sourceAllocation, CommandBufferType from, CommandBufferType to)
    {
        if (from == to) return;
        _pendingTransfers.Enqueue((sourceAllocation, from, to));
    }

    public void FlushTransfers(int limit = 0)
    {
        if (_pendingTransfers.Count == 0) return;

        using var o = _opaque.DeferMerge();
        using var t = _transparent.DeferMerge();

        var count = 0;
        while (_pendingTransfers.Count > 0 && (limit == 0 || count < limit))
        {
            var (sourceAlloc, fromType, toType) = _pendingTransfers.Dequeue();
            var sourceBuffer = GetBuffer(fromType);
            var targetBuffer = GetBuffer(toType);

            var targetAlloc = targetBuffer.AddRange(new DrawElementsIndirectCommand[sourceAlloc.Length]);
            targetBuffer.CopyFrom(sourceBuffer, sourceAlloc, targetAlloc);

            // only remove from source if transferring between opaque/transparent (not copying to mask)
            if ((fromType == CommandBufferType.Opaque && toType == CommandBufferType.Transparent) ||
                (fromType == CommandBufferType.Transparent && toType == CommandBufferType.Opaque))
            {
                sourceBuffer.Remove(sourceAlloc);
            }

            count++;
        }

        Log.Debug("Flushed {Count} command transfers ({Remaining} remaining)", count, _pendingTransfers.Count);
    }

    public BufferAllocation CopyToMask(DrawIndirectBuffer sourceBuffer, BufferAllocation sourceAllocation)
    {
        var maskAllocation = _mask.AddRange(new DrawElementsIndirectCommand[sourceAllocation.Length]);
        _mask.CopyFrom(sourceBuffer, sourceAllocation, maskAllocation);

        return maskAllocation;
    }

    public BufferAllocation[] CopyToMask(DrawIndirectBuffer sourceBuffer, BufferAllocation[] sourceAllocations)
    {
        var maskAllocations = new BufferAllocation[sourceAllocations.Length];
        for (int i = 0; i < sourceAllocations.Length; i++)
        {
            maskAllocations[i] = CopyToMask(sourceBuffer, sourceAllocations[i]);
        }
        return maskAllocations;
    }

    public void RemoveFromMask(BufferAllocation[] maskAllocations)
    {
        _mask.RemoveRange(maskAllocations);
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
