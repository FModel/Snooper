using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public abstract class SkinnedMeshComponent : MeshComponent
{
    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh.Materials, transform, skeletalMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, USkinnedMeshComponent component) : base(skeletalMesh.Materials, component)
    {
        ObjectPath = skeletalMesh.GetPathName();

        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    internal sealed override string Icon => "\uf5d7";
}
