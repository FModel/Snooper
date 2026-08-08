using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Assets.Exports;

namespace Snooper.Extensions;

public static class ObjectExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetCleanPath(this UObject owner) => owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName());
}
