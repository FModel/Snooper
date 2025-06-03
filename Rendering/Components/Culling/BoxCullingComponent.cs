using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Components.Culling;

public class BoxCullingComponent : CullingComponent
{
    public readonly Vector3 Center;
    public readonly Vector3 Extents;
    
    public BoxCullingComponent(Vector3 center, Vector3 extents)
    {
        Center = center;
        Extents = extents;
    }

    public BoxCullingComponent(FBox box)
    {
        box *= Settings.GlobalScale;
        box.GetCenterAndExtents(out var center, out var extents);

        Center = new Vector3(center.X, center.Z, center.Y);
        Extents = new Vector3(extents.X, extents.Z, extents.Y);
    }

    public void Update(CameraComponent cameraComponent)
    {
        var frustum = cameraComponent.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }

        var center = Vector3.Transform(Center, Actor?.Transform.WorldMatrix ?? Matrix4x4.Identity);
        foreach (var plane in frustum)
        {
            var distance = Vector3.Dot(plane.Normal, center) + plane.D;
            var radius = Vector3.Dot(Extents, Vector3.Abs(plane.Normal));
            if (distance < -radius)
            {
                Actor.IsVisible = false;
                return;
            }
        }

        Actor.IsVisible = true;
    }
}
