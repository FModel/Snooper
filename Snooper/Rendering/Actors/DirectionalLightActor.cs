using System.Numerics;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class DirectionalLightActor : Actor
{
    public DirectionalLightActor(string name, Vector3 direction, float intensity, Vector3 color) : base(name)
    {
        Components.Add(new ArrowComponent(new Transform(new Quaternion(direction, 1.0f)), "Light Direction"));
        Components.Add(new DirectionalLightComponent(intensity, color, null, "Directional Light"));
    }
}
