using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Culling;

public class SphereCullingComponent : BoxCullingComponent
{
    public readonly Vector3 Origin;
    public readonly float Radius;

    public SphereCullingComponent(FBoxSphereBounds sphere) : base(sphere.GetBox())
    {
        var origin = sphere.Origin * Settings.GlobalScale;
        var radius = sphere.SphereRadius * Settings.GlobalScale;

        Origin = new Vector3(origin.X, origin.Z, origin.Y);
        Radius = radius;
    }
}
