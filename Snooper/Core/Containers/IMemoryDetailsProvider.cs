using Snooper.Core.Containers.Buffers;

namespace Snooper.Core.Containers;

public interface IMemoryDetailsProvider : IBufferStatisticsProvider
{
    public IEnumerable<MemoryDetail> GetMemoryDetails();
}

public record MemoryDetail(string Name, string Type, long Allocated, long Used, IBufferStatisticsProvider? Provider = null)
{
    public MemoryDetail(string name, string type, IMemorySizeProvider provider) : this(name, type, provider.Allocated, provider.Used)
    {
        
    }
    
    public MemoryDetail(string name, string type, IBufferStatisticsProvider provider) : this(name, type, provider.Allocated, provider.Used, provider)
    {
        
    }
    
    public MemoryDetail(string name, IMemorySizeProvider provider) : this(name, provider.GetType().Name, provider)
    {
        
    }
    
    public MemoryDetail(string name, IBufferStatisticsProvider provider) : this(name, provider.GetType().Name, provider)
    {
        
    }
    
    public long Wasted => Allocated - Used;
    public double UsagePercentage => Allocated > 0 ? (double)Used / Allocated * 100.0 : 0.0;
}