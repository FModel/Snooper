using CUE4Parse.GameTypes.Nascar.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Houdini;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(StaticMeshRenderSystem))]
public class StaticMeshComponent : MeshComponent
{
    private StaticMeshComponent(StaticMeshComponent other) : base(other)
    {

    }

    public StaticMeshComponent(UStaticMesh staticMesh, Transform? transform = null) : base(staticMesh.Materials, transform, staticMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    public StaticMeshComponent(UIRMesh staticMesh, uint meshIndex, FPackageIndex?[]? materials = null, Transform? transform = null) : base(materials ?? staticMesh.Materials, transform, meshIndex > 0 ? $"{staticMesh.Name}_{meshIndex}" : staticMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, meshIndex, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    public StaticMeshComponent(UHoudiniStaticMesh staticMesh, Transform? transform = null) : base(staticMesh.Materials, transform, staticMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(staticMesh.Materials, component)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(staticMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));

        // TODO: use component.LODData to override some stuff (eg vertex colors)
    }

    protected StaticMeshComponent(FPackageIndex?[] materials, UMeshComponent component) : base(materials, component)
    {

    }

    protected StaticMeshComponent(FPackageIndex?[] materials, Transform? transform = null, string? name = null) : base(materials, transform, name)
    {

    }

    public override string Icon => Settings.CubeIcon;

    public override object Clone() => new StaticMeshComponent(this);
}
