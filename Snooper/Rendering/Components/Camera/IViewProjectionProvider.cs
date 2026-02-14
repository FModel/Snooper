using System.Numerics;

namespace Snooper.Rendering.Components.Camera;

public interface IViewProjectionProvider
{
    public Matrix4x4 ViewMatrix { get; }
    public Matrix4x4 ProjectionMatrix { get; }
}
