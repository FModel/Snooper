using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using Serilog;
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

    public override void Update(CameraComponent cameraComponent)
    {
        var frustum = cameraComponent.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }
        
        var minIndex = int.MaxValue;
        var maxIndex = int.MinValue;

        Matrix4x4[] matrices = [Actor.Transform.WorldMatrix, ..Actor.InstancedTransforms.WorldMatrix];
        for (var i = 0; i < matrices.Length; i++)
        {
            var matrix = matrices[i];
            var center = Vector3.Transform(Center, matrix);
            
            var visible = true;
            foreach (var plane in frustum)
            {
                var distance = Vector3.Dot(plane.Normal, center) + plane.D;
                var radius = Vector3.Dot(Extents, Vector3.Abs(plane.Normal));
                if (distance < -radius)
                {
                    visible = false;
                    break;
                }
            }
            
            if (visible)
            {
                minIndex = Math.Min(minIndex, i);
                maxIndex = Math.Max(maxIndex, i);
            }
        }

        if (minIndex == int.MaxValue || maxIndex == int.MinValue)
        {
            Actor.VisibleInstances = new Range(0, 0);
        }
        else
        {
            Actor.VisibleInstances = new Range(minIndex, maxIndex + 1);
        }
    }

    public override float GetScreenSpaceCoverage(CameraComponent cameraComponent)
    {
        var screenCorners = GetScreenCorners(cameraComponent);

        Vector2 screenMin = new(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new(float.MinValue, float.MinValue);

        foreach (var corner in screenCorners)
        {
            screenMin = Vector2.Min(screenMin, corner);
            screenMax = Vector2.Max(screenMax, corner);
        }

        var width = screenMax.X - screenMin.X;
        var height = screenMax.Y - screenMin.Y;

        return Math.Clamp(Math.Max(width, height), 0, 1);
    }

    private Vector3[] GetWorldCorners()
    {
        var worldMatrix = Actor?.Transform.WorldMatrix ?? Matrix4x4.Identity;
        var corners = new Vector3[8];

        corners[0] = Vector3.Transform(Center + new Vector3(Extents.X, Extents.Y, Extents.Z), worldMatrix);
        corners[1] = Vector3.Transform(Center + Extents with { Z = -Extents.Z }, worldMatrix);
        corners[2] = Vector3.Transform(Center + Extents with { Y = -Extents.Y }, worldMatrix);
        corners[3] = Vector3.Transform(Center + new Vector3(Extents.X, -Extents.Y, -Extents.Z), worldMatrix);
        corners[4] = Vector3.Transform(Center + Extents with { X = -Extents.X }, worldMatrix);
        corners[5] = Vector3.Transform(Center + new Vector3(-Extents.X, Extents.Y, -Extents.Z), worldMatrix);
        corners[6] = Vector3.Transform(Center + new Vector3(-Extents.X, -Extents.Y, Extents.Z), worldMatrix);
        corners[7] = Vector3.Transform(Center + new Vector3(-Extents.X, -Extents.Y, -Extents.Z), worldMatrix);

        return corners;
    }

    private Vector2[] GetScreenCorners(CameraComponent cameraComponent)
    {
        var corners = GetWorldCorners();
        var screenCorners = new Vector2[corners.Length];

        for (int i = 0; i < corners.Length; i++)
        {
            screenCorners[i] = cameraComponent.ProjectToScreen(corners[i]);
        }

        return screenCorners;
    }
}
