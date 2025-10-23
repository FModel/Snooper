using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Descriptors;

public readonly struct CullingBounds
{
    public readonly Vector3 Center;
    public readonly Vector3 Extents;

    public CullingBounds(Vector3 center, Vector3 extents)
    {
        Center = center;
        Extents = extents;
    }
    
    public CullingBounds(Vector3 extents)
    {
        Center = Vector3.Zero;
        Extents = extents;
    }
    
    public CullingBounds(float sphereRadius)
    {
        Center = Vector3.Zero;
        Extents = new Vector3(sphereRadius);
    }

    public CullingBounds(FBox box)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);
        
        Center = new Vector3(center.X, center.Z, center.Y);
        Extents = new Vector3(extents.X, extents.Z, extents.Y);
    }
    
    public static implicit operator CullingBounds(FBox box) => new(box);

    public override string ToString() => $"Center: {Center}, Extents: {Extents}";
}