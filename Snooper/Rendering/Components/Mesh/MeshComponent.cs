using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using ImGuiNET;
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
    public long Specular { get; init; }
}

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerDrawMeshData>
{
    public sealed override PrimitiveSection[] Sections { get; }

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
                            parameters.TryGetTexture2d(out var normal, CMaterialParams2.Normals[0]) &&
                            parameters.TryGetTexture2d(out var specular, CMaterialParams2.SpecularMasks[0]))
                        {
                            Sections[index].DrawDataContainer = new DrawDataContainer(new Texture2D(diffuse), new Texture2D(normal), new Texture2D(specular));
                        }
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
    
    private class DrawDataContainer(Texture diffuse, Texture normal, Texture specular) : IDrawDataContainer
    {
        private BindlessTexture? _diffuse;
        private BindlessTexture? _normal;
        private BindlessTexture? _specular;
        
        public bool HasTextures => true;
        
        public Dictionary<string, Texture> GetTextures() => new()
        {
            ["Diffuse"] = diffuse,
            ["Normal"] = normal,
            ["Specular"] = specular,
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
                case "Specular":
                    _specular = bindless;
                    break;
                default:
                    throw new ArgumentException($"Unknown texture key: {key}");
            }
        }

        public void FinalizeGpuData()
        {
            if (_diffuse is null || _normal is null || _specular is null)
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }
            
            _diffuse.Generate();
            _diffuse.MakeResident();
            _normal.Generate();
            _normal.MakeResident();
            _specular.Generate();
            _specular.MakeResident();

            Raw = new PerDrawMeshData
            {
                IsReady = true,
                Diffuse = _diffuse,
                Normal = _normal,
                Specular = _specular,
            };
        }

        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            var largest = ImGui.GetContentRegionAvail();
            largest.X -= ImGui.GetScrollX();
            largest.X /= 3;
            
            ImGui.Image(diffuse.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
            ImGui.SameLine();
            ImGui.Image(normal.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
            ImGui.SameLine();
            ImGui.Image(specular.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
        }
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
