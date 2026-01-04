using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public interface IShadowSupportedSystem
{
    public void RenderShadows(CameraComponent light);
}
