using System.Numerics;

namespace Snooper.Extensions;

public static class NumericExtensions
{
    public static string FormatTime(this float milliseconds)
    {
        return milliseconds switch
        {
            < 1.0f => $"{milliseconds:F3} ms",
            < 1000.0f => $"{milliseconds:F2} ms",
            < 60000.0f => $"{milliseconds / 1000.0f:F2} s",
            < 3600000.0f => $"{milliseconds / 60000.0f:F2} m",
            _ => $"{milliseconds / 3600000.0f:F2} h"
        };
    }

    public static Vector3 AsTint(this Vector3 color)
    {
        var brightest = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        return brightest > 0.0f ? color / brightest : Vector3.One;
    }
}
