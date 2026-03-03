using System.Numerics;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Components.Visualization;

public class InstancedMeshBoundsVisualization : DebugComponent
{
    private readonly InstancedStaticMeshComponent _owner;

    public InstancedMeshBoundsVisualization(InstancedStaticMeshComponent owner) : base(owner.IsVisible ? Settings.VisibleMeshBounds : Settings.HiddenMeshBounds, name: $"{owner.Name} (Instance Bounds)")
    {
        _owner = owner;
        Descriptor = new PrimitiveDescriptor<Vector3>(owner.Descriptor.Bounds, () => new Geometry(owner.Descriptor.Bounds));
    }

    protected override int InstanceCount => _owner.LocalInstancedTransforms.Count;

    public override Matrix4x4[] GetWorldMatrices(int index = -1) => _owner.GetWorldMatrices(index);

    private class Geometry : DebugGeometry
    {
        public Geometry(CullingBounds bounds)
        {
            var c = bounds.Center;
            var e = bounds.Extents;

            var c0 = new Vector3(c.X - e.X, c.Y - e.Y, c.Z - e.Z);
            var c1 = new Vector3(c.X + e.X, c.Y - e.Y, c.Z - e.Z);
            var c2 = new Vector3(c.X + e.X, c.Y + e.Y, c.Z - e.Z);
            var c3 = new Vector3(c.X - e.X, c.Y + e.Y, c.Z - e.Z);
            var c4 = new Vector3(c.X - e.X, c.Y - e.Y, c.Z + e.Z);
            var c5 = new Vector3(c.X + e.X, c.Y - e.Y, c.Z + e.Z);
            var c6 = new Vector3(c.X + e.X, c.Y + e.Y, c.Z + e.Z);
            var c7 = new Vector3(c.X - e.X, c.Y + e.Y, c.Z + e.Z);

            Vertices = [c0, c1, c2, c3, c4, c5, c6, c7];

            Indices =
            [
                0, 1,  1, 2,  2, 3,  3, 0, // bottom face
                4, 5,  5, 6,  6, 7,  7, 4, // top face
                0, 4,  1, 5,  2, 6,  3, 7, // verticals
            ];
        }
    }
}
