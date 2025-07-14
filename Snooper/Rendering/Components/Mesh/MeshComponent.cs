using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerInstanceMeshData : IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
    public long Diffuse { get; set; }
    public Vector2 _padding;
}

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : TPrimitiveComponent<Vertex, PerInstanceMeshData>
{
    public abstract int LodCount { get; }
    public abstract float[] ScreenSizes { get; }

    public readonly CMaterialParams2 MaterialParameters;
    
    public sealed override MeshMaterialSection[] MaterialSections { get; protected init; }

    protected MeshComponent(CBaseMeshLod lod, ResolvedObject[] materials) : base(new Geometry(lod))
    {
        MaterialSections = new MeshMaterialSection[lod.Sections.Value.Length];
        for (var i = 0; i < MaterialSections.Length; i++)
        {
            var s = lod.Sections.Value[i];
            var materialIndex = s.MaterialIndex;
            MaterialSections[i] = new MeshMaterialSection(materialIndex, s.FirstIndex, s.NumFaces * 3);
        }

        var section = MaterialSections.MaxBy(x => x.IndexCount);
        MaterialParameters = new CMaterialParams2();
        
        if (section.MaterialIndex >= 0 && section.MaterialIndex < materials.Length)
        {
            if (materials[section.MaterialIndex].TryLoad(out var m) && m is UMaterialInterface material)
            {
                material.GetParams(MaterialParameters, EMaterialFormat.FirstLayer);
            }
        }
        else
        {
            Log.Warning("MeshComponent: Material index {0} out of bounds for mesh {1}", section.MaterialIndex, Actor?.Name);
        }
    }

    protected override bool ApplyInstanceData(PerInstanceMeshData[] data)
    {
        MaterialParameters.TryGetTexture2d(out var diffuse, CMaterialParams2.FallbackDiffuse);
        if (diffuse is null) MaterialParameters.TryGetFirstTexture2d(out diffuse);
        
        var bindless = new BindlessTexture(new Texture2D(diffuse));
        bindless.Generate();
        bindless.MakeResident();
        
        for (var i = 0; i < data.Length; i++)
        {
            data[i].Diffuse = bindless;
        }

        return true;
    }

    protected override void CopyCachedData(PerInstanceMeshData[] data, PerInstanceMeshData[] cached)
    {
        for (var i = 0; i < data.Length; i++)
        {
            data[i].Diffuse = cached[i].Diffuse;
        }
    }

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
