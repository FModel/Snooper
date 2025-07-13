using Snooper.Rendering.Components;

namespace Snooper.Rendering.Actors;

public class SkyboxActor : Actor
{
    public SkyboxActor() : base(System.Guid.NewGuid(), "Skybox")
    {
        Components.Add(new CubeComponent());
    }
}