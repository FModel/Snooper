using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Core;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(SkinnedMeshRenderSystem))]
public abstract class SkinnedMeshComponent : MeshComponent
{
    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh.Materials, transform, skeletalMesh.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    protected SkinnedMeshComponent(USkeleton skeleton, Transform? transform = null) : base([], transform, skeleton.Name)
    {
        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeleton, () => new Geometry(skeleton));
    }

    protected SkinnedMeshComponent(USkeletalMesh skeletalMesh, USkinnedMeshComponent component) : base(skeletalMesh.Materials, component)
    {
        ObjectPath = skeletalMesh.GetPathName();

        Descriptor = PrimitiveDescriptor<Vertex>.GetOrCreate(skeletalMesh, (vertices, indices, colors, extraUvs) => new Geometry(vertices, indices, colors, extraUvs));
    }

    internal sealed override string Icon => "\uf5d7";
}
