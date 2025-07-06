using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public interface ISystem<in T> : IDisposable
{
    public void Load();
    public void Update(float delta);
    public void Render(T generic);
}

public interface IGameSystem : ISystem<CameraComponent>;