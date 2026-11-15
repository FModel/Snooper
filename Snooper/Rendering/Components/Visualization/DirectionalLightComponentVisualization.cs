using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Components.Visualization;

public class DirectionalLightComponentVisualization : ArrowComponent, IVisualization<DirectionalLightComponentVisualization>
{
    public static DirectionalLightComponentVisualization[] Create(ActorComponent owner) => owner is DirectionalLightComponent light ? [new DirectionalLightComponentVisualization(light)] : [];

    private DirectionalLightComponentVisualization(DirectionalLightComponent light) : base(Settings.DirectionalLight, null, $"{light.Name} (Direction)")
    {

    }
}
