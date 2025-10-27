namespace Snooper.Core.Containers;

public interface IMemoryDetailsProvider : IMemorySizeProvider
{
    IEnumerable<MemoryDetail> GetMemoryDetails();
}

public record MemoryDetail(string Name, string Type, long Allocated, long Used, IMemoryDetailsProvider? Provider = null)
{
    public long Wasted => Allocated - Used;
    public double UsagePercentage => Allocated > 0 ? (double)Used / Allocated * 100.0 : 0.0;
    
    public bool HasChildren => Provider != null;
}

