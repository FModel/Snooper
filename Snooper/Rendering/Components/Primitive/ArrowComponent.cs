using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Components.Primitive;

public class ArrowComponent : DebugComponent
{
    public ArrowComponent(UArrowComponent component) : base(component)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => new Geometry(Vector3.Zero, component.ArrowLength * Settings.GlobalScale));

        var color = new Vector3(component.ArrowColor.R / 255.0f, component.ArrowColor.G / 255.0f, component.ArrowColor.B / 255.0f);
        Materials[0].InlineContainer = new MaterialDataContainer(color, 3.0f);
    }

    public ArrowComponent(Vector3? color = null, Transform? transform = null, string? name = null) : base(color ?? new Vector3(1.0f, 0.0f, 0.0f), 2, transform, name)
    {
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(), () => new Geometry(Vector3.Zero, 1.5f));
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(Vector3 center, float length)
        {
            var axisLength = length * 0.3f;
            var coneHeight = length * 0.25f;
            var coneRadius = length * 0.1f;
            var shaftLength = length - coneHeight;
            var shaftEnd = center + new Vector3(shaftLength, 0, 0);
            var arrowTip = center + new Vector3(length, 0, 0);

            var vertices = new List<Vector3>
            {
                center,
                center + new Vector3(0, axisLength, 0),
                center,
                center + new Vector3(0, 0, axisLength),
                center,
                shaftEnd
            };

            var coneBasePoints = new[]
            {
                shaftEnd + new Vector3(0, coneRadius, 0),
                shaftEnd + new Vector3(0, 0, coneRadius),
                shaftEnd + new Vector3(0, -coneRadius, 0),
                shaftEnd + new Vector3(0, 0, -coneRadius)
            };

            foreach (var point in coneBasePoints)
            {
                vertices.Add(point);
                vertices.Add(arrowTip);
            }

            for (var i = 0; i < coneBasePoints.Length; i++)
            {
                var next = (i + 1) % coneBasePoints.Length;
                vertices.Add(coneBasePoints[i]);
                vertices.Add(coneBasePoints[next]);
            }

            Vertices = vertices.ToArray();

            Indices = new uint[Vertices.Length];
            for (uint i = 0; i < Indices.Length; i++)
            {
                Indices[i] = i;
            }
        }
    }
}
