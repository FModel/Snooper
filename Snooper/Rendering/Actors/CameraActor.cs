using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Actors;

public class CameraActor : Actor
{
    public CameraComponent CameraComponent { get; }
    
    public CameraActor(string name) : base(name)
    {
        CameraComponent = new CameraComponent();
        
        Components.Add(CameraComponent);
        // Components.Add(new CameraFrustumComponent(CameraComponent));
    }

    internal override string Icon => "video";
}
