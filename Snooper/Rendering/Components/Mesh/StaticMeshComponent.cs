using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(UStaticMesh staticMesh, Transform? transform = null) : base(staticMesh.Materials, transform, staticMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(staticMesh.Materials, component)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));

        // TODO: use component.LODData to override some stuff (eg vertex colors)
    }

    internal override string Icon => "sphere";
}
