using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public struct PerDrawMeshData : IPerDrawData
{
    public bool IsReady { get; init; }
    public int Padding { get; init; }
    public long Diffuse { get; init; }
    public long Normal { get; init; }
}

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : TPrimitiveComponent<Vertex, PerInstanceData, PerDrawMeshData>
{
    public sealed override PrimitiveSection[] Sections { get; protected init; }

    protected MeshComponent(CBaseMeshLod lod, ResolvedObject?[] materials) : base(new Geometry(lod))
    {
        Sections = new PrimitiveSection[lod.Sections.Value.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            var index = i;
            var s = lod.Sections.Value[index];
            Sections[index] = new PrimitiveSection(s.FirstIndex, s.NumFaces * 3);
            
            // TODO: do somewhere else
            Task.Run(() =>
            {
                var materialIndex = s.MaterialIndex;
                if (materialIndex >= 0 && materialIndex < materials.Length)
                {
                    if (materials[materialIndex]?.TryLoad(out var m) == true && m is UUnrealMaterial material)
                    {
                        var parameters = new CMaterialParams2();
                        material.GetParams(parameters, EMaterialFormat.FirstLayer);

                        if (parameters.TryGetTexture2d(out var diffuse, CMaterialParams2.Diffuse[0]) &&
                            parameters.TryGetTexture2d(out var normal, CMaterialParams2.Normals[0]))
                        {
                            Sections[index].DrawDataContainer = new DrawDataContainer(new Texture2D(diffuse), new Texture2D(normal));
                        }
                        
                        // if (Sections[index].DrawDataContainer is null && parameters.TryGetFirstTexture2d(out var fallback))
                        // {
                        //     Log.Warning("No valid textures found for material at index {MatIndex}.", materialIndex);
                        //     Sections[index].DrawDataContainer = new DrawDataContainer(new Texture2D(fallback), new ColorTexture(FColor.Gray));
                        // }
                    }
                    else
                    {
                        Log.Warning("Material at index {MatIndex} is not valid or could not be loaded.", materialIndex);
                    }
                }
                else
                {
                    Log.Warning("Material index {MatIndex} is out of bounds for mesh component with {MaterialsLength} materials.", materialIndex, materials.Length);
                }
            });
        }
    }

    protected abstract IVertexData GetPrimitive(int index);
    
    private class DrawDataContainer(Texture diffuse, Texture normal) : IDrawDataContainer
    {
        private BindlessTexture? _diffuse;
        private BindlessTexture? _normal;
        
        public Dictionary<string, Texture> GetTextures() => new()
        {
            ["Diffuse"] = diffuse,
            ["Normal"] = normal
        };

        public void SetBindlessTexture(string key, BindlessTexture bindless)
        {
            switch (key)
            {
                case "Diffuse":
                    _diffuse = bindless;
                    break;
                case "Normal":
                    _normal = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            _diffuse!.Generate();
            _normal!.Generate();
            
            _diffuse.MakeResident();
            _normal.MakeResident();
            
            Raw = new PerDrawMeshData
            {
                IsReady = true,
                Diffuse = _diffuse,
                Normal = _normal
            };
        }

        public IPerDrawData? Raw { get; private set; }
    }
    
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
