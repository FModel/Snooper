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

    private Buffer<DrawElementsIndirectCommand>.DeferMergeScope? _opaqueScope;
    private Buffer<DrawElementsIndirectCommand>.DeferMergeScope? _transparentScope;

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
        _mask.Allocate(10);

        Log.Debug("Allocated CommandBufferSet: {OpaqueCapacity} opaque, {TransparentCapacity} transparent, {MaskCapacity} mask", _opaque.Capacity, _transparent.Capacity, _mask.Capacity);
    }

    public DrawIndirectBuffer GetBuffer(CommandBufferType type) => type switch
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

    public BufferAllocation[] Transfer(BufferAllocation[] sourceAllocations, CommandBufferType from, CommandBufferType to)
    {
        if (from == to) return sourceAllocations;

        var sourceBuffer = GetBuffer(from);
        var targetBuffer = GetBuffer(to);

        // we allocate space for all commands at once to avoid multiple resizes
        // that batch allocation will then be split into individual allocations for each command, which are returned to the caller
        // it works because each allocation has length 1 (commands are added one by one)
        var totalCommands = sourceAllocations.Length;
        var batchAllocation = targetBuffer.AddRange(new DrawElementsIndirectCommand[totalCommands]);

        var targetAllocations = new BufferAllocation[totalCommands];
        for (var i = 0; i < totalCommands; i++)
        {
            var sourceAllocation = sourceAllocations[i];
            targetAllocations[i] = new BufferAllocation(batchAllocation.AllocationId + i, batchAllocation.StartIndex + i, sourceAllocation.Length);

            targetBuffer.CopyFrom(sourceBuffer, sourceAllocation, targetAllocations[i]);

            // only remove from source if transferring between opaque/transparent (not copying to mask)
            if ((from == CommandBufferType.Opaque && to == CommandBufferType.Transparent) ||
                (from == CommandBufferType.Transparent && to == CommandBufferType.Opaque))
            {
                sourceBuffer.Remove(sourceAllocation);
            }
        }

        // TODO: if totalCommands > 1, what we return here is unusable for any operation
        // this is because our buffer has its own private list of allocations
        // and by manually splitting the batch allocation into individual allocations, those individual allocations are not registered in the buffer's list of allocations
        // for now this is fine but it might become a problem later on (eg on removal)
        return targetAllocations;
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
