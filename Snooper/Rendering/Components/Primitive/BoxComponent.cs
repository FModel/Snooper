using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Primitive;

public class BoxComponent : ShapeComponent
{
    public BoxComponent(UBoxComponent component) : base(component)
    {
        Color ??= new Vector3(0.45f, 0.15f, 0.15f);

        var extent = Vector3.One / 2;
        if (component.TryGetValue(out FVector boxExtent, "BoxExtent"))
        {
            extent = new Vector3(boxExtent.X, boxExtent.Z, boxExtent.Y) * Settings.GlobalScale;
        }

        var bounds = new CullingBounds(extent);
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(bounds));

        Materials[0].InlineContainer = new MaterialDataContainer(Color.Value, LineThickness);
    }

    public BoxComponent(Vector3 extents, Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : this(Vector3.Zero, extents, color, lineThickness, transform, name)
    {

    }

    public BoxComponent(Vector3 center, Vector3 extents, Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : this(new CullingBounds(center, extents), color, lineThickness, transform, name)
    {

    }

    public BoxComponent(CullingBounds bounds, Vector3 color, float lineThickness = 1.0f, Transform? transform = null, string? name = null) : base(color, lineThickness, transform, name)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(bounds));
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(CullingBounds bounds) : this(bounds.Center, bounds.Extents)
        {

        }

        public Geometry(Vector3 center, Vector3 extents)
        {
            var c0 = new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z - extents.Z);
            var c1 = new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z - extents.Z);
            var c2 = new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z - extents.Z);
            var c3 = new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z - extents.Z);
            var c4 = new Vector3(center.X - extents.X, center.Y - extents.Y, center.Z + extents.Z);
            var c5 = new Vector3(center.X + extents.X, center.Y - extents.Y, center.Z + extents.Z);
            var c6 = new Vector3(center.X + extents.X, center.Y + extents.Y, center.Z + extents.Z);
            var c7 = new Vector3(center.X - extents.X, center.Y + extents.Y, center.Z + extents.Z);

            Vertices =
            [
                c0, c1,
                c0, c3,
                c0, c4,
                c1, c2,
                c1, c5,
                c2, c6,
                c3, c2,
                c3, c7,
                c4, c5,
                c4, c7,
                c5, c6,
                c7, c6
            ];

            Indices =
            [
                0, 1,
                2, 3,
                4, 5,
                6, 7,
                8, 9,
                10, 11,
                12, 13,
                14, 15,
                16, 17,
                18, 19,
                20, 21,
                22, 23
            ];
        }
    }
}
