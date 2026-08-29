using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;

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

public readonly struct ShadowRenderContext(IEnumerable<IMeshRenderSystem> systems) : IRenderContext
{
    public readonly IEnumerable<IMeshRenderSystem> Systems = systems;
}

public readonly struct CullRenderContext(IEnumerable<IGeometryRenderSystem> systems, ReadOnlyMemory<CullView> views) : IRenderContext
{
    public readonly IEnumerable<IGeometryRenderSystem> Systems = systems;
    public readonly ReadOnlyMemory<CullView> Views = views;
}
