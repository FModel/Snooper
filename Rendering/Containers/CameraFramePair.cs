using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Buffers;

namespace Snooper.Rendering.Containers;

public readonly struct CameraFramePair(CameraComponent camera) : IEquatable<CameraFramePair>, IResizable
{
    public GeometryBuffer DeferredPass { get; } = new(1, 1);
    public SsaoFramebuffer SsaoPass { get; } = new(1, 1);
    public FxaaFramebuffer ForwardPass { get; } = new(1, 1);
    public PostProcessingFramebuffer Framebuffer { get; } = new(1, 1);
    public CameraComponent Camera { get; } = camera;

    public void Generate(int width, int height)
    {
        DeferredPass.Generate();
        SsaoPass.Generate();
        ForwardPass.Generate();
        Framebuffer.Generate();
        Resize(width, height);
    }

    public void Resize(int newWidth, int newHeight)
    {
        DeferredPass.Resize(newWidth, newHeight);
        SsaoPass.Resize(newWidth, newHeight);
        ForwardPass.Resize(newWidth, newHeight);
        Framebuffer.Resize(newWidth, newHeight);
    }

    public bool Equals(CameraFramePair other) =>
        DeferredPass.Equals(other.DeferredPass) &&
        SsaoPass.Equals(other.SsaoPass) &&
        ForwardPass.Equals(other.ForwardPass) &&
        Framebuffer.Equals(other.Framebuffer) &&
        Camera.Equals(other.Camera);
    public override bool Equals(object? obj) => obj is CameraFramePair other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(DeferredPass, SsaoPass, ForwardPass, Framebuffer, Camera);

    public static bool operator ==(CameraFramePair left, CameraFramePair right) => left.Equals(right);
    public static bool operator !=(CameraFramePair left, CameraFramePair right) => !(left == right);
}
