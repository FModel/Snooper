using Snooper.Core.Containers.Buffers;

namespace Snooper.Core.Containers;

public interface IMemoryDetailsProvider : IBufferStatisticsProvider
{
    public IEnumerable<MemoryDetail> GetMemoryDetails();
}

public readonly struct MemoryDetail(string name, string type, long allocated, long used, IBufferStatisticsProvider? provider = null)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly long Allocated = allocated;
    public readonly long Used = used;
    public readonly IBufferStatisticsProvider? Provider = provider;

    public long Wasted => Allocated - Used;
    public double UsagePercentage => Allocated > 0 ? (double)Used / Allocated * 100.0 : 0.0;

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
}
