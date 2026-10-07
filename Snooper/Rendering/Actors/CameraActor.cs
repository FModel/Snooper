using System.Numerics;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class CameraActor : Actor
{
    public InteractiveCameraComponent CameraComponent { get; }

    public CameraActor() : base("Camera")
    {
        CameraComponent = new InteractiveCameraComponent(new Transform(new Vector3(0, 1.14f, 3.5f), new Vector3(180, 18, 0)));

        Components.Add(CameraComponent);
    }
}
