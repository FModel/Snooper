namespace Snooper.Core.Containers.Buffers;

public interface IBufferStatisticsProvider : IMemorySizeProvider
{
    public BufferStatistics? GetBufferStatistics() => null;
}

public readonly struct BufferStatistics(int capacity, int usedItems, int freeItems, IReadOnlyList<BufferAllocationMetadata> allocations, IReadOnlyList<FreeBlock> freeBlocks, double fragmentationPercentage)
{
    public readonly int Capacity = capacity;
    public readonly int UsedItems = usedItems;
    public readonly int FreeItems = freeItems;
    public readonly IReadOnlyList<BufferAllocationMetadata> Allocations = allocations;
    public readonly IReadOnlyList<FreeBlock> FreeBlocks = freeBlocks;
    public readonly double FragmentationPercentage = fragmentationPercentage;
}
