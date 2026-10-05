using System.Numerics;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class SunActor : Actor
{
    public SunActor() : base("Sun Light")
    {
        const float distance = 5;

        Components.Add(new DirectionalLightComponent(10.0f, new Vector3(1.00f, 0.96f, 0.90f), new Transform(new Vector3(-distance, 0, distance), new Vector3(140, 37, 0)), "Directional Light"));
    }
}
