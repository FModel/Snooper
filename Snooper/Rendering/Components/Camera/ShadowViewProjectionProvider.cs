using System.Numerics;

namespace Snooper.Rendering.Components.Camera;

public readonly struct ShadowViewProjectionProvider(Matrix4x4 view, Matrix4x4 projection) : IViewProjectionProvider
{
    public Matrix4x4 ViewMatrix { get; } = view;
    public Matrix4x4 ProjectionMatrix { get; } = projection;
}
