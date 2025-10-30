namespace Snooper.Core.Containers.Buffers;

public interface IBufferStatisticsProvider : IMemorySizeProvider
{
    public BufferStatistics? GetBufferStatistics() => null;
}

public record BufferStatistics(
    int Capacity,
    int UsedItems,
    int FreeItems,
    IReadOnlyList<BufferAllocationMetadata> Allocations,
    IReadOnlyList<FreeBlock> FreeBlocks,
    double FragmentationPercentage
);