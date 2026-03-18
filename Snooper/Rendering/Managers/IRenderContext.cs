using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public interface IRenderContext;

public readonly struct NoRenderContext : IRenderContext;

public readonly struct SystemRenderContext(CameraComponent camera, IEnumerable<IRenderSystem> systems) : IRenderContext
{
    public readonly CameraComponent Camera = camera;
    public readonly IEnumerable<IRenderSystem> Systems = systems;
}

public readonly struct ShadowRenderContext(CameraComponent camera, DirectionalLightComponent light, IEnumerable<IShadowSystem> systems) : IRenderContext
{
    public readonly CameraComponent Camera = camera;
    public readonly DirectionalLightComponent Light = light;
    public readonly IEnumerable<IShadowSystem> Systems = systems;
}
