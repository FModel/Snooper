using System.Numerics;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Components.Visualization;

public class MeshBoundsVisualization : DebugComponent
{
    private readonly MeshComponent _owner;
    private readonly bool _posed;

    public MeshBoundsVisualization(MeshComponent owner) : base(owner.IsVisible ? Settings.VisibleMeshBounds : Settings.HiddenMeshBounds, name: $"{owner.Name} (Bounds)")
    {
        _owner = owner;
        _posed = owner is SkinnedMeshComponent;

        var bounds = _posed ? new CullingBounds(Vector3.One) : owner.Bounds;
        Descriptor = new PrimitiveDescriptor<Vector3>(bounds, () => new Geometry(bounds));
    }

    public override int InstanceCount => _owner.InstanceCount;

    public override Matrix4x4[] GetWorldMatrices(int index = -1)
    {
        var matrices = _owner.GetWorldMatrices(index);
        if (!_posed) return matrices;

        var bounds = _owner.Bounds;
        var box = Matrix4x4.CreateScale(bounds.Extents) * Matrix4x4.CreateTranslation(bounds.Center);
        for (var i = 0; i < matrices.Length; i++)
        {
            matrices[i] = box * matrices[i];
        }
        return matrices;
    }

    private class Geometry : DebugGeometry
    {
        public Geometry(CullingBounds bounds)
        {
            var c = bounds.Center;
            var e = bounds.Extents;

            Vertices =
            [
                c - e,
                c + new Vector3(e.X, -e.Y, -e.Z),
                c + e with { Z = -e.Z },
                c + new Vector3(-e.X, e.Y, -e.Z),
                c + new Vector3(-e.X, -e.Y, e.Z),
                c + e with { Y = -e.Y },
                c + e,
                c + e with { X = -e.X }
            ];

            Indices =
            [
                0, 1,  1, 2,  2, 3,  3, 0,
                4, 5,  5, 6,  6, 7,  7, 4,
                0, 4,  1, 5,  2, 6,  3, 7,
            ];
        }
    }
}
