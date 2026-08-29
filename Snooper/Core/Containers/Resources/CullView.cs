using System.Numerics;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Containers.Resources;

public readonly struct CullView(IViewProjectionProvider view, CameraComponent lodReference)
{
    public readonly Matrix4x4 ViewProjection = view.ViewMatrix * view.ProjectionMatrix;
    public readonly Vector3 LodReferencePosition = lodReference.InverseViewMatrix.Translation;
    public readonly float LodProjectionScale = lodReference.ProjectionMatrix.M22;
}
