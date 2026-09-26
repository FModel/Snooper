using System.Runtime.CompilerServices;

namespace Snooper.Core.Containers;

[InlineArray(Settings.MaxNumberOfLods)]
public struct LodArray<T> where T : unmanaged
{
    private T _first;
}

[InlineArray(Settings.MaxWeightmaps)]
public struct WeightmapArray<T> where T : unmanaged
{
    private T _first;
}

[InlineArray(4)]
public struct FixedArray4<T> where T : unmanaged
{
    private T _first;
}
