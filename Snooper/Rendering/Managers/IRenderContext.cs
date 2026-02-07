using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public interface IRenderContext;

public sealed record NoRenderContext : IRenderContext;

public sealed record SystemRenderContext(
    CameraComponent Camera,
    IEnumerable<ActorSystem> Systems
) : IRenderContext;

public sealed record ShadowRenderContext(
    CameraComponent Camera,
    DirectionalLightComponent Light,
    IEnumerable<IShadowSystem> Systems
) : IRenderContext;
