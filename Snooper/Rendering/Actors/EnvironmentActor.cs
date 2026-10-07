using System.Numerics;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Skybox;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class EnvironmentActor : Actor
{
    public EnvironmentActor() : base("Environment")
    {
        const float distance = 5;
        const float spacing = 1;

        Components.Add(new SkyAtmosphereComponent(new Transform(new Vector3(-distance + spacing, 0, distance)), "Sky Atmosphere"));
        Components.Add(new ExponentialHeightFogComponent(new Vector3(0.447f, 0.638f, 1.0f), transform: new Transform(new Vector3(spacing, 0, 0)), name: "Height Fog"));
        Components.Add(new SkyLightComponent(new Vector3(0.6f, 0.7f, 0.8f), new Vector3(0.4f, 0.35f, 0.3f), 1.5f, new Transform(new Vector3(spacing * 2, 0, 0)), "Sky Light"));
    }
}
