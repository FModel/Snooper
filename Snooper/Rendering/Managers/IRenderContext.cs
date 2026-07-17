using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;

namespace Snooper.Rendering.Managers;

public interface IRenderContext;

public readonly struct NoRenderContext : IRenderContext;

public readonly struct GeometryRenderContext(CameraComponent camera, IEnumerable<IGeometryRenderSystem> systems) : IRenderContext
{
    public readonly CameraComponent Camera = camera;
    public readonly IEnumerable<IGeometryRenderSystem> Systems = systems;
}

public readonly struct ComputeRenderContext(CameraComponent camera, IEnumerable<IComputeRenderSystem> systems) : IRenderContext
{
    public readonly CameraComponent Camera = camera;
    public readonly IEnumerable<IComputeRenderSystem> Systems = systems;
}

public readonly struct ShadowRenderContext(CameraComponent camera, DirectionalLightComponent light, IEnumerable<IMeshRenderSystem> systems) : IRenderContext
{
    public readonly CameraComponent Camera = camera;
    public readonly DirectionalLightComponent Light = light;
    public readonly IEnumerable<IMeshRenderSystem> Systems = systems;
}
