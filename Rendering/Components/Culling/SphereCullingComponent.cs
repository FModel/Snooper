using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Components.Culling;

public class SphereCullingComponent : CullingComponent
{
    public readonly Vector3 Origin;
    public readonly float Radius;

    public SphereCullingComponent(FBoxSphereBounds bounds)
    {
        Origin = new Vector3(bounds.Origin.X, bounds.Origin.Z, bounds.Origin.Y) * Settings.GlobalScale;
        Radius = bounds.SphereRadius * Settings.GlobalScale;
    }

    public float GetScreenSpaceCoverage(CameraComponent cameraComponent)
    {
        // Calculate the 8 corners of the bounding box that encapsulates the sphere
        var actorWorldPosition = Actor.Transform.WorldMatrix;
        Vector3[] corners = new Vector3[8];
        corners[0] = actorWorldPosition.Translation + new Vector3(Radius, Radius, Radius);
        corners[1] = actorWorldPosition.Translation + new Vector3(Radius, Radius, -Radius);
        corners[2] = actorWorldPosition.Translation + new Vector3(Radius, -Radius, Radius);
        corners[3] = actorWorldPosition.Translation + new Vector3(Radius, -Radius, -Radius);
        corners[4] = actorWorldPosition.Translation + new Vector3(-Radius, Radius, Radius);
        corners[5] = actorWorldPosition.Translation + new Vector3(-Radius, Radius, -Radius);
        corners[6] = actorWorldPosition.Translation + new Vector3(-Radius, -Radius, Radius);
        corners[7] = actorWorldPosition.Translation + new Vector3(-Radius, -Radius, -Radius);

        // Project the corners to screen space
        Vector2[] screenCorners = new Vector2[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            screenCorners[i] = ProjectToScreen(corners[i], cameraComponent.ViewProjectionMatrix, cameraComponent.AspectRatio);
        }

        // Find the min and max screen coordinates
        Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);

        foreach (Vector2 corner in screenCorners)
        {
            screenMin.X = Math.Min(screenMin.X, corner.X);
            screenMin.Y = Math.Min(screenMin.Y, corner.Y);
            screenMax.X = Math.Max(screenMax.X, corner.X);
            screenMax.Y = Math.Max(screenMax.Y, corner.Y);
        }

        // Calculate the width and height of the bounding box in screen space
        float width = screenMax.X - screenMin.X;
        float height = screenMax.Y - screenMin.Y;

        // Calculate the area of the bounding box in screen space
        float screenArea = width * height;

        // Calculate the percentage of the screen covered by the sphere
        float screenCoverage = screenArea;

        // Normalize the coverage to a percentage of the screen
        // Assuming the screen dimensions are normalized to 1x1
        return Math.Min(screenCoverage, 1.0f); // Ensure it does not exceed 1.0 (100%)
    }

    private Vector2 ProjectToScreen(Vector3 worldPosition, Matrix4x4 viewProjectionMatrix, float aspectRatio)
    {
        // Transform the world position to clip space
        Vector4 clipSpacePosition = Vector4.Transform(new Vector4(worldPosition, 1.0f), viewProjectionMatrix);

        // Perspective division to get NDC (Normalized Device Coordinates)
        Vector3 ndcSpacePosition = new Vector3(clipSpacePosition.X, clipSpacePosition.Y, clipSpacePosition.Z) / clipSpacePosition.W;

        // Convert NDC to screen space
        Vector2 screenSpacePosition = new Vector2(
            (ndcSpacePosition.X + 1.0f) * 0.5f, // Normalize to [0, 1]
            (1.0f - (ndcSpacePosition.Y + 1.0f) * 0.5f) // Normalize to [0, 1] and adjust for aspect ratio
        );

        return screenSpacePosition;
    }
}
