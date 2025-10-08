using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : MeshComponent
{
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh) : base(skeletalMesh.Materials, null, skeletalMesh.Name)
    {
        Descriptor = new PrimitiveDescriptor2<Vertex>(skeletalMesh, (vertices, indices) => new Geometry(vertices, indices));
    }
    
    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(skeletalMesh.Materials, component)
    {
        Descriptor = new PrimitiveDescriptor2<Vertex>(skeletalMesh, (vertices, indices) => new Geometry(vertices, indices));
    }
}
