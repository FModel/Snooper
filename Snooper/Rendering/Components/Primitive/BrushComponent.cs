using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Primitive;

public class BrushComponent : DebugComponent
{
    public BrushComponent(UBrushComponent component) : base(component)
    {
        if (component.GetBrush() is { } brush)
        {
            Descriptor = new PrimitiveDescriptor<Vector3>(brush.Bounds.GetBox(), () => new Geometry(brush));
        }

        Materials[0].MaterialDataContainer = new MaterialDataContainer(new Vector3(0.75f, 0, 0));
    }
}
