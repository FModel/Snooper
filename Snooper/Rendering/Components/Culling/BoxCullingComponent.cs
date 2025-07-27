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
    
    private const int CornerCount = 8;

    public override void CheckForVisibility(Plane[] frustum)
    {
        var minIndex = int.MaxValue;
        var maxIndex = int.MinValue;
        
        Span<Vector3> corners = stackalloc Vector3[CornerCount];
        Span<Vector3> localCorners = stackalloc Vector3[CornerCount];
        for (var j = 0; j < CornerCount; j++)
        {
            localCorners[j] = GetLocalCorner(j);
        }

        var matrices = Actor.GetWorldMatrices();
        for (var i = 0; i < matrices.Length; i++)
        {
            var matrix = matrices[i];
            var visible = true;

            for (var j = 0; j < CornerCount; j++)
            {
                corners[j] = Vector3.Transform(localCorners[j], matrix);
            }

            foreach (var plane in frustum)
            {
                var outside = 0;
                for (var j = 0; j < CornerCount; j++)
                {
                    var distance = Vector3.Dot(plane.Normal, corners[j]) + plane.D;
                    if (distance < 0f)
                        outside++;
                }

                if (outside == CornerCount)
                {
                    visible = false;
                    break;
                }
            }

            if (visible)
            {
                if (i < minIndex) minIndex = i;
                if (i > maxIndex) maxIndex = i;
            }
        }

        Actor.VisibleInstances = minIndex <= maxIndex ? new Range(minIndex, maxIndex + 1) : new Range(0, 0);
    }
    
    private Vector3 GetLocalCorner(int i)
        => Center + new Vector3(
            (i & 1) == 0 ? Extents.X : -Extents.X,
            (i & 2) == 0 ? Extents.Y : -Extents.Y,
            (i & 4) == 0 ? Extents.Z : -Extents.Z
        );

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
        for (var i = 0; i < corners.Length; i++)
        {
            corners[i] = Vector3.Transform(GetLocalCorner(i), worldMatrix);
        }

        return corners;
    }

    private Vector2[] GetScreenCorners(CameraComponent cameraComponent)
    {
        var corners = GetWorldCorners();
        var screenCorners = new Vector2[corners.Length];

        for (var i = 0; i < corners.Length; i++)
        {
            screenCorners[i] = cameraComponent.ProjectToScreen(corners[i]);
        }

        return screenCorners;
    }
}
