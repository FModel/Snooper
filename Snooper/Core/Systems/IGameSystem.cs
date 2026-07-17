using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Core.Systems;

public interface IGameSystem : IDisposable
{
    public void Load();
    public void Update(float delta);
}

/// <summary>
/// systems that do per-frame render-time work (gpu compute, context updates) but draw nothing
/// </summary>
public interface IComputeRenderSystem : IGameSystem
{
    public void Execute(CameraComponent camera);
}

/// <summary>
/// systems that actually draw something to the screen
/// </summary>
public interface IGeometryRenderSystem : IGameSystem
{
    public void Render(CameraComponent camera, CommandBufferType type);
}

/// <summary>
/// systems that draw meshes to the screen, thus support shadow rendering too
/// </summary>
public interface IMeshRenderSystem : IGeometryRenderSystem
{
    public void RenderShadowCascade(IViewProjectionProvider cascade);
    public IEnumerable<MeshComponent> GetMeshComponents();
}
