using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public interface IShadowSystem
{
    public void RenderShadows(IViewProjectionProvider[] cascades);
}
