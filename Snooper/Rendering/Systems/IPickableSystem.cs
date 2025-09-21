using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public interface IPickableSystem
{
    public void RenderPicking(CameraComponent camera);
}