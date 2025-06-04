using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Containers;

public readonly struct CameraFramePair(Framebuffer framebuffer, CameraComponent camera) : IEquatable<CameraFramePair>
{
    public Framebuffer Framebuffer { get; } = framebuffer;
    public CameraComponent Camera { get; } = camera;

    public bool Equals(CameraFramePair other) => Framebuffer.Equals(other.Framebuffer) && Camera.Equals(other.Camera);
    public override bool Equals(object? obj) => obj is CameraFramePair other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Framebuffer, Camera);

    public static bool operator ==(CameraFramePair left, CameraFramePair right) => left.Equals(right);
    public static bool operator !=(CameraFramePair left, CameraFramePair right) => !(left == right);
}
