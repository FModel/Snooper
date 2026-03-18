using System.Numerics;

namespace Snooper.Rendering.Components.Camera;

public readonly struct ShadowViewProjectionProvider(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix) : IViewProjectionProvider
{
    public Matrix4x4 ViewMatrix { get; } = viewMatrix;
    public Matrix4x4 ProjectionMatrix { get; } = projectionMatrix;
    public Matrix4x4 InverseViewMatrix { get; } = Matrix4x4.Invert(viewMatrix, out var inverse) ? inverse : Matrix4x4.Identity;
    public Matrix4x4 InverseProjectionMatrix { get; } = Matrix4x4.Invert(projectionMatrix, out var inverse) ? inverse : Matrix4x4.Identity;
}
