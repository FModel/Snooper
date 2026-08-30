using System.Numerics;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Containers.Resources;

public readonly struct CullView
{
    public readonly Matrix4x4 ViewProjection;
    public readonly Vector3 LodReferencePosition;
    public readonly float LodProjectionScale;
    public readonly float LodOrthoExtent;

    public CullView(IViewProjectionProvider view, CameraComponent lodReference)
    {
        ViewProjection = view.ViewMatrix * view.ProjectionMatrix;
        LodReferencePosition = lodReference.InverseViewMatrix.Translation;
        LodProjectionScale = lodReference.ProjectionMatrix.M22;
        LodOrthoExtent = 0.0f;
    }

    public CullView(ShadowMapView view, CameraComponent distanceReference)
    {
        ViewProjection = view.ViewProjection;
        LodReferencePosition = distanceReference.InverseViewMatrix.Translation;
        LodProjectionScale = 0.0f;
        LodOrthoExtent = view.OrthoExtent;
    }
}
