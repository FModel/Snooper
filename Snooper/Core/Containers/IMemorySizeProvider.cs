using Snooper.Extensions;

namespace Snooper.Core.Containers;

public interface IMemorySizeProvider
{
    public long Allocated { get; }
    public long Used { get; }
    
    public string GetFormattedSpace() => Used.GetReadableSizeOutOf(Allocated);
    
    public double UsagePercentage => Allocated > 0 ? (double)Used / Allocated * 100.0 : 0.0;
    public long Wasted => Allocated - Used;
}