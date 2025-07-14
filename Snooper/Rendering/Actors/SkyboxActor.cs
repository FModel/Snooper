using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Skybox;

namespace Snooper.Rendering.Actors;

public class SkyboxActor : Actor
{
    public CubeComponent SkyboxComponent { get; }
    
    public SkyboxActor() : base(System.Guid.NewGuid(), "Skybox")
    {
        SkyboxComponent = new AtmosphericComponent();
        
        Components.Add(SkyboxComponent);
    }

    internal override string Icon => "sun";
}