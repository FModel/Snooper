using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Culling;

public class BoxCullingComponent : CullingComponent
{
    public readonly Vector3 BoxCenter;
    public readonly Vector3 BoxExtents;

    protected BoxCullingComponent(IPrimitiveData primitive, FBox box) : base(primitive)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);

        BoxCenter = new Vector3(center.X, center.Z, center.Y);
        BoxExtents = new Vector3(extents.X, extents.Z, extents.Y);
    }

    public override void Update(CameraComponent cameraComponent)
    {
        var frustum = cameraComponent.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }

        var center = Vector3.Transform(BoxCenter, Actor?.Transform.WorldMatrix ?? Matrix4x4.Identity);
        foreach (var plane in frustum)
        {
            var distance = Vector3.Dot(plane.Normal, center) + plane.D;
            var radius = Vector3.Dot(BoxExtents, Vector3.Abs(plane.Normal));
            if (distance < -radius)
            {
                IsVisible = false;
                return;
            }
        }

        IsVisible = true;
    }
}
