using System.Numerics;

namespace Snooper.Rendering.Components.Camera;

public record ShadowViewProjectionProvider(Matrix4x4 ViewMatrix, Matrix4x4 ProjectionMatrix) : IViewProjectionProvider
{
    public Matrix4x4 InverseViewMatrix { get; } = Matrix4x4.Invert(ViewMatrix, out var inverse) ? inverse : Matrix4x4.Identity;
    public Matrix4x4 InverseProjectionMatrix { get; } = Matrix4x4.Invert(ProjectionMatrix, out var inverse) ? inverse : Matrix4x4.Identity;
}
