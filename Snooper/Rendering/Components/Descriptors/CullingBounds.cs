using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Descriptors;

[StructLayout(LayoutKind.Sequential)]
public readonly struct CullingBounds
{
    public readonly Vector3 Center;
    public readonly Vector3 Extents;

    public CullingBounds(Vector3 center, Vector3 extents)
    {
        Center = center;
        Extents = extents;
    }

    public CullingBounds(Vector3 extents) : this(Vector3.Zero, extents)
    {

    }

    public CullingBounds(float sphereRadius) : this(new Vector3(sphereRadius))
    {

    }

    public CullingBounds(FBox box)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);

        Center = new Vector3(center.X, center.Z, center.Y);
        Extents = new Vector3(extents.X, extents.Z, extents.Y);
    }

    public string BoundsFormatted => GetBoundsFormatted();

    private string GetBoundsFormatted()
    {
        var size = Extents * 2;
        var absX = Math.Abs(size.X);
        var absY = Math.Abs(size.Y);
        var absZ = Math.Abs(size.Z);

        var unit = absX >= 1000 || absY >= 1000 || absZ >= 1000 ? "km" : "m";
        var factor = unit == "km" ? 0.001f : 1f;
        return string.Format(CultureInfo.InvariantCulture, "{0:F2}{3} x {1:F2}{3} x {2:F2}{3}", absX * factor, absY * factor, absZ * factor, unit);
    }

    public static implicit operator CullingBounds(FBox box) => new(box);

    public override string ToString() => $"Center: {Center}, Extents: {Extents}";
}
