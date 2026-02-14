using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public interface IGameSystem : IDisposable
{
    public void Load();
    public void Update(float delta);
}

public interface IRenderSystem : IGameSystem
{
    public void Render(CameraComponent camera, CommandBufferType type);
}
