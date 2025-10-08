using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Mesh;

public class StaticMeshComponent : MeshComponent
{
    public StaticMeshComponent(UStaticMesh staticMesh) : base(staticMesh.Materials, null, staticMesh.Name)
    {
        Descriptor = new PrimitiveDescriptor2<Vertex>(staticMesh, (vertices, indices) => new Geometry(vertices, indices));
    }
    
    public StaticMeshComponent(UStaticMesh staticMesh, UStaticMeshComponent component) : base(staticMesh.Materials, component)
    {
        Descriptor = new PrimitiveDescriptor2<Vertex>(staticMesh, (vertices, indices) => new Geometry(vertices, indices));
        
        // TODO: use component.LODData to override some stuff (eg vertex colors)
    }
}
