using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using Snooper.Core;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : TPrimitiveComponent<Vertex>
{
    public abstract int LodCount { get; }
    public abstract float[] ScreenSizes { get; }
    
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; }

    protected MeshComponent(CBaseMeshLod lod) : base(new Geometry(lod))
    {
        MaterialSections = new MeshMaterialSection[lod.Sections.Value.Length];
        for (var i = 0; i < MaterialSections.Length; i++)
        {
            var section = lod.Sections.Value[i];
            MaterialSections[i] = new MeshMaterialSection(section.MaterialIndex, section.FirstIndex, section.NumFaces * 3);
        }
    }

    // public override void Update(IndirectResources<Vertex> resources)
    // {
    //     base.Update(resources);
    //
    //     if (LODCount < 2 || ScreenSizes.Length < 2 ||
    //         Actor is not MeshActor { ActorManager: SceneSystem { ActiveCamera: {} camera }, IsVisible: true } actor)
    //         return;
    //
    //     CurrentScreenSize = actor.CullingComponent.GetScreenSpaceCoverage(camera);
    //
    //     var currentLODIndex = LODIndex;
    //     for (var i = 0; i < ScreenSizes.Length; i++)
    //     {
    //         if (CurrentScreenSize >= ScreenSizes[i])
    //         {
    //             currentLODIndex = i;
    //             break;
    //         }
    //     }
    //
    //     if (currentLODIndex != LODIndex && currentLODIndex >= 0 && currentLODIndex < LODCount)
    //     {
    //         Log.Debug("{0}: Screen Size: {1}, Switching LOD from {2} to {3}", actor.Name, CurrentScreenSize, LODIndex, currentLODIndex);
    //         LODIndex = currentLODIndex;
    //
    //         var primitive = GetPrimitive(LODIndex);
    //         resources.UpdatePrimitive(DrawId, primitive.Indices, primitive.Vertices);
    //     }
    // }

    protected abstract IVertexData GetPrimitive(int index);
    
    protected readonly struct Geometry : IVertexData
    {
        public Vertex[] Vertices { get; }
        public uint[] Indices { get; }
        
        public Geometry(CBaseMeshLod lod)
        {
            var vertices = lod switch
            {
                CStaticMeshLod staticLod => staticLod.Verts,
                CSkelMeshLod skelLod => skelLod.Verts,
                _ => throw new NotSupportedException($"Unsupported mesh type: {lod.GetType().Name}")
            };

            Vertices = new Vertex[vertices.Length];
            for (var i = 0; i < Vertices.Length; i++)
            {
                var vertex = vertices[i];
                var position = new Vector3(vertex.Position.X, vertex.Position.Z, vertex.Position.Y) * Settings.GlobalScale;
                var normal = new Vector3(vertex.Normal.X, vertex.Normal.Z, vertex.Normal.Y);
                var tangent = new Vector3(vertex.Tangent.X, vertex.Tangent.Z, vertex.Tangent.Y);
                var texCoord = new Vector2(vertex.UV.U, vertex.UV.V);

                Vertices[i] = new Vertex(position, normal, tangent, texCoord);
            }

            Indices = new uint[lod.Indices.Value.Length];
            for (var i = 0; i < Indices.Length; i++)
            {
                Indices[i] = (uint) lod.Indices.Value[i];
            }
        }
    }
}
