using System.Numerics;
using System.Runtime.CompilerServices;

namespace Snooper.Extensions;

public static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetReadableSize<T>(this T size) where T : INumber<T>
    {
        if (size == T.Zero) return "0 B";

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var converted = double.CreateChecked(size);
        while (converted >= 1024 && order < sizes.Length - 1)
        {
            order++;
            converted /= 1024;
        }

        return $"{converted:F2} {sizes[order]}".TrimStart();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetReadableSizeOutOf<T>(this T value, T total) where T : INumber<T>
    {
        if (total == T.Zero) return "0 B";
        if (value == T.Zero) return "0 B";

        var ratio = value.GetReadableRatio(total);
        var valueParts = value.GetReadableSize().Split(' ');
        var totalParts = total.GetReadableSize().Split(' ');
        
        return valueParts[1] != totalParts[1] ?
            $"{valueParts[0]} {valueParts[1]} / {totalParts[0]} {totalParts[1]} ({ratio})" :
            $"{valueParts[0]} / {totalParts[0]} {valueParts[1]} ({ratio})";
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetReadableRatio<T>(this T value, T total) where T : INumber<T>
    {
        if (total == T.Zero) return "0%";
        if (value == T.Zero) return "0%";

        var ratio = double.CreateChecked(value) / double.CreateChecked(total) * 100;
        return $"{ratio:F2}%";
    }
}