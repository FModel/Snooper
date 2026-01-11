using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Primitive;

public class ArrowComponent : DebugComponent
{
    public ArrowComponent(UArrowComponent component) : base(component)
    {
        // TODO:
    }

    public ArrowComponent(Transform? transform = null, string? name = null) : base(new Vector3(1.0f, 0.0f, 0.0f), 1, transform, name)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => new Geometry(Vector3.Zero, 0.1f, 3f, 1f, 0.25f));
    }
}
