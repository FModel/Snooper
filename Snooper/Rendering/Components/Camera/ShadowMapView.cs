using System.Numerics;

namespace Snooper.Rendering.Components.Camera;

public readonly struct ShadowMapView : IViewProjectionProvider
{
    public Matrix4x4 ViewMatrix { get; }
    public Matrix4x4 ProjectionMatrix { get; }
    public Matrix4x4 InverseViewMatrix { get; }
    public Matrix4x4 InverseProjectionMatrix { get; }
    public Matrix4x4 ViewProjection { get; }

    public int Slot { get; }
    public float OrthoExtent { get; }
    public float TexelWorldSize { get; }
    public float DepthScale { get; }
    public float SplitFar { get; }

    public int ViewIndex => Slot + 1;

    public ShadowMapView(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, int slot, float orthoExtent, float texelWorldSize, float depthScale, float splitFar)
    {
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;
        InverseViewMatrix = Matrix4x4.Invert(viewMatrix, out var inverseView) ? inverseView : Matrix4x4.Identity;
        InverseProjectionMatrix = Matrix4x4.Invert(projectionMatrix, out var inverseProjection) ? inverseProjection : Matrix4x4.Identity;
        ViewProjection = viewMatrix * projectionMatrix;

        Slot = slot;
        OrthoExtent = orthoExtent;
        TexelWorldSize = texelWorldSize;
        DepthScale = depthScale;
        SplitFar = splitFar;
    }
}
