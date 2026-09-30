using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Managers;

public class Viewport(InteractiveCameraComponent camera) : IResizable
{
    public InteractiveCameraComponent Camera { get; } = camera;

    public void Resize(int newWidth, int newHeight)
    {
        Camera.Resize(newWidth, newHeight);
    }
}
