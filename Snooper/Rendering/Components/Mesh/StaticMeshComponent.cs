using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(StaticMeshRenderSystem))]
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

    public override string Icon => "\uf1b2";
}
