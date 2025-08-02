using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Objects.Core.Math;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

public readonly struct Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord)
{
    public readonly Vector3 Position = position;
    public readonly Vector3 Normal = normal;
    public readonly Vector3 Tangent = tangent;
    public readonly Vector2 TexCoord = texCoord;
}

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
    protected MeshComponent(IReadOnlyList<CBaseMeshLod> levels, ResolvedObject?[] materials, FBox box) : base(CreateGeometry(levels), box)
    {
        for (var i = 0; i < Sections.Length; i++)
        {
            var index = i;
            
            // TODO: do somewhere else
            Task.Run(() =>
            {
                var materialIndex = Sections[index].MaterialIndex;
                if (materialIndex < materials.Length)
                {
                    if (materials[materialIndex]?.TryLoad(out var m) == true && m is UUnrealMaterial material)
                    {
                        var parameters = new CMaterialParams2();
                        material.GetParams(parameters, EMaterialFormat.FirstLayer);

                        if (parameters.TryGetTexture2d(out var diffuse, CMaterialParams2.Diffuse[0]))
                        {
                            parameters.TryGetTexture2d(out var normal, CMaterialParams2.Normals[0]);
                            parameters.TryGetTexture2d(out var specular, CMaterialParams2.SpecularMasks[0]);

                            Sections[index].DrawDataContainer = new DrawDataContainer(
                                new Texture2D(diffuse),
                                normal != null ? new Texture2D(normal) : null,
                                specular != null ? new Texture2D(specular) : null);
                        }
                        else if (parameters.TryGetFirstTexture2d(out var fallback))
                        {
                            Sections[index].DrawDataContainer = new DrawDataContainer(new Texture2D(fallback), null, null);
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

    private static LevelOfDetail<Vertex>[] CreateGeometry(IReadOnlyList<CBaseMeshLod> levels)
    {
        var geometries = new LevelOfDetail<Vertex>[levels.Count];
        for (var i = 0; i < geometries.Length; i++)
        {
            var sections = new PrimitiveSectionDescriptor[levels[i].Sections.Value.Length];
            for (var j = 0; j < sections.Length; j++)
            {
                var section = levels[i].Sections.Value[j];
                sections[j] = new PrimitiveSectionDescriptor((uint)section.FirstIndex, (uint)section.NumFaces * 3, (uint)section.MaterialIndex);
            }
            
            geometries[i] = new LevelOfDetail<Vertex>(new Geometry(levels[i]), sections);
        }
        return geometries;
    }

    private class DrawDataContainer(Texture diffuse, Texture? normal, Texture? specular) : IDrawDataContainer
    {
        private BindlessTexture? _diffuse;
        private BindlessTexture? _normal;
        private BindlessTexture? _specular;
        
        public bool HasTextures => true;

        public Dictionary<string, Texture> GetTextures()
        {
            var dict = new Dictionary<string, Texture>
            {
                ["Diffuse"] = diffuse
            };
            if (normal != null) dict["Normal"] = normal;
            if (specular != null) dict["Specular"] = specular;
            return dict;
        }

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
            if (_diffuse is null)
            {
                throw new InvalidOperationException("Unset textures. Ensure that SetBindlessTexture is called for all textures.");
            }
            
            _diffuse.Generate();
            _diffuse.MakeResident();
            
            if (_normal != null)
            {
                _normal.Generate();
                _normal.MakeResident();
            }

            if (_specular != null)
            {
                _specular.Generate();
                _specular.MakeResident();
            }

            Raw = new PerDrawMeshData
            {
                IsReady = true,
                Diffuse = _diffuse,
                Normal = _normal ?? 0L,
                Specular = _specular ?? 0L,
            };
        }

        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            // var largest = ImGui.GetContentRegionAvail();
            // largest.X -= ImGui.GetScrollX();
            // largest.X /= 3;
            //
            // ImGui.Image(diffuse.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
            // ImGui.SameLine();
            // ImGui.Image(normal.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
            // ImGui.SameLine();
            // ImGui.Image(specular.GetPointer(), new Vector2(largest.X), Vector2.Zero, Vector2.One);
        }
    }

    private readonly struct Geometry : TPrimitiveData<Vertex>
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
