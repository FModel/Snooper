namespace Snooper.Core.Systems;

public interface IGameSystem : IDisposable
{
    public void Load();
    public void Update(float delta);
    public void Render();
}
