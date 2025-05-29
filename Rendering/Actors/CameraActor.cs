using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Actors;

public class CameraActor : Actor
{
    public CameraActor(string name) : base(name)
    {
        Transform.Scale *= 0.25f;

        var cameraComponent = new CameraComponent();
        Components.Add(cameraComponent);
        Components.Add(new CameraFrustumComponent(cameraComponent));
    }
}
