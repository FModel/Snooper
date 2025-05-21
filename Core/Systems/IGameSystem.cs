using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public interface IGameSystem : IDisposable
{
    public void Load();
    public void Update(float delta);
    public void Render(CameraComponent camera);
}
