using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Primitive;

public struct CullingBounds
{
    public readonly Vector3 Center;
    public float Padding0;
    public readonly Vector3 Extents;
    public uint MaxLevelOfDetail;

    public CullingBounds(FBox box)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);
        
        Center = new Vector3(center.X, center.Z, center.Y);
        Extents = new Vector3(extents.X, extents.Z, extents.Y);
    }
    
    public static implicit operator CullingBounds(FBox box) => new(box);
}