using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Buffers;

namespace Snooper.Rendering.Containers;

public readonly struct CameraFramePair(CameraComponent camera) : IEquatable<CameraFramePair>, IResizable
{
    public GeometryBuffer GBuffer { get; } = new(1, 1); // deferred rendering
    public MsaaFramebuffer MsaaBuffer { get; } = new(1, 1); // forward rendering
    public FullQuadFramebuffer Framebuffer { get; } = new(1, 1); // post-processing
    public CameraComponent Camera { get; } = camera;

    public void Generate(int width, int height)
    {
        GBuffer.Generate();
        MsaaBuffer.Generate();
        Framebuffer.Generate();
        Resize(width, height);
    }

    public void Resize(int newWidth, int newHeight)
    {
        GBuffer.Resize(newWidth, newHeight);
        MsaaBuffer.Resize(newWidth, newHeight);
        Framebuffer.Resize(newWidth, newHeight);
    }

    public bool Equals(CameraFramePair other) =>
        GBuffer.Equals(other.GBuffer) &&
        MsaaBuffer.Equals(other.MsaaBuffer) &&
        Framebuffer.Equals(other.Framebuffer) &&
        Camera.Equals(other.Camera);
    public override bool Equals(object? obj) => obj is CameraFramePair other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(GBuffer, MsaaBuffer, Framebuffer, Camera);

    public static bool operator ==(CameraFramePair left, CameraFramePair right) => left.Equals(right);
    public static bool operator !=(CameraFramePair left, CameraFramePair right) => !(left == right);
}
