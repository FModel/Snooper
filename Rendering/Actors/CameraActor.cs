using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Actors;

public class CameraActor : Actor
{
    public CameraActor(string name) : base(name)
    {
        var cameraComponent = new CameraComponent { FarPlaneDistance = 3.0f, FrustumCullingEnabled = true };
        Components.Add(cameraComponent);
        Components.Add(new CameraFrustumComponent(cameraComponent));
    }
}
