using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Actors;

public class CameraActor : Actor
{
    public InteractiveCameraComponent CameraComponent { get; }

    public CameraActor(string name) : base(name)
    {
        CameraComponent = new InteractiveCameraComponent();

        Components.Add(CameraComponent);
    }
}
