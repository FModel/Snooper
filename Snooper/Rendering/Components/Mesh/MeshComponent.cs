using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
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
    public uint IsTranslucent;
    public ulong Diffuse;
    public ulong Normal;
    public ulong Specular;
    public Vector2 Roughness;
    public Vector2 Padding1;
    public Vector3 DiffuseColor;
}

[DefaultActorSystem(typeof(RenderSystem))]
[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerDrawMeshData>
{
    protected ResolvedObject?[] MaterialsToParse { get; init; } = [];

    protected MeshComponent(ResolvedObject?[] materials, Transform? transform = null, string? name = null) : base(transform, name)
    {
        MaterialsToParse = materials;
    }

    protected MeshComponent(UMeshComponent component) : base(component)
    {
        
    }

    protected override void OnAddedToActor()
    {
        base.OnAddedToActor();

        for (var i = 0; i < Materials.Length; i++)
        {
            var index = i;
            
            // TODO: do somewhere else
            Task.Run(() =>
            {
                var materialIndex = Materials[index].MaterialIndex;
                if (MaterialsToParse[materialIndex]?.TryLoad(out var m) == true && m is UUnrealMaterial material)
                {
                    var parameters = new CMaterialParams2();
                    material.GetParams(parameters, EMaterialFormat.FirstLayer);

                    Materials[index].DrawDataContainer = ParseMaterialParameters(parameters, material.Owner.Provider.ProjectName.ToUpperInvariant());
                }
                else
                {
                    Log.Warning("Material at index {MatIndex} is not valid or could not be loaded.", materialIndex);
                }
            });
        }
    }
    
    private DrawDataContainer? ParseMaterialParameters(CMaterialParams2 parameters, string projectName)
    {
        UTexture? diffuse = null, normal = null, specular = null;
        var diffuseColor = Vector3.One;
        var roughness = Vector2.UnitY;

        var layer = 0;
        for (var i = 0; i < CMaterialParams2.Diffuse.Length; i++)
        {
            if (parameters.TryGetTexture2d(out diffuse, CMaterialParams2.Diffuse[i]))
            {
                layer = i;
                break;
            }
        }

        if (diffuse == null)
        {
            layer = 0;
            parameters.TryGetTexture2d(out diffuse, CMaterialParams2.FallbackDiffuse);
        }

        if (diffuse != null)
        {
            if (parameters.TryGetLinearColor(out var color, CMaterialParams2.DiffuseColors[layer]))
            {
                color = color.ToSRGB();
                diffuseColor = new Vector3(color.R, color.G, color.B);
            }
            
            parameters.TryGetTexture2d(out normal, [..CMaterialParams2.Normals[layer], CMaterialParams2.FallbackNormals]);
            
            parameters.TryGetTexture2d(out specular, [..CMaterialParams2.SpecularMasks[layer], CMaterialParams2.FallbackSpecularMasks]);
            if (parameters.TryGetScalar(out var roughnessMin, "RoughnessMin", "SpecRoughnessMin"))
                roughness.X = roughnessMin;
            if (parameters.TryGetScalar(out var roughnessMax, "RoughnessMax", "SpecRoughnessMax"))
                roughness.Y = roughnessMax;
        }
        else
        {
            parameters.TryGetFirstTexture2d(out diffuse);
        }
        
        if (diffuse == null)
            return null;

        Texture2D? specularTex = null;
        if (specular != null)
        {
            specularTex = new Texture2D(specular);
            if ((parameters.TryGetSwitch(out var srg, "SwizzleRoughnessToGreen") && srg) || parameters.Textures.ContainsKey("SRM"))
            {
                specularTex.SwizzleMask =
                [
                    (int) PixelFormat.Red,
                    (int) PixelFormat.Blue,
                    (int) PixelFormat.Green,
                    (int) PixelFormat.Alpha
                ];
            }
            else
            {
                specularTex.SwizzlePerGame(projectName);
            }
        }

        return new DrawDataContainer(
            new Texture2D(diffuse),
            normal != null ? new Texture2D(normal) : null,
            specularTex,
            roughness,
            diffuseColor,
            parameters.BlendMode is EBlendMode.BLEND_Translucent or EBlendMode.BLEND_Masked
        );
    }

    protected LevelOfDetail<Vertex>[] CreateGeometry(FGuid guid, IReadOnlyList<CBaseMeshLod> levels)
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
            
            geometries[i] = new LevelOfDetail<Vertex>(guid, new Geometry(levels[i]), levels[i].ScreenSize, sections);
        }
        return geometries;
    }

    private class DrawDataContainer(Texture diffuse, Texture? normal, Texture? specular, Vector2? roughness = null, Vector3? diffuseColor = null, bool translucent = false) : IDrawDataContainer
    {
        private BindlessTexture? _diffuse;
        private BindlessTexture? _normal;
        private BindlessTexture? _specular;
        
        public bool HasTextures => true;
        public bool IsTranslucent { get; } = translucent;

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
                IsTranslucent = IsTranslucent ? 1u : 0,
                Diffuse = _diffuse,
                Normal = _normal ?? 0UL,
                Specular = _specular ?? 0UL,
                Roughness = roughness ?? Vector2.UnitY,
                DiffuseColor = diffuseColor ?? Vector3.One
            };
        }

        public IPerDrawData? Raw { get; private set; }
        
        public void DrawControls()
        {
            
        }

        public void Dispose()
        {
            _diffuse?.Dispose();
            _normal?.Dispose();
            _specular?.Dispose();
            
            _diffuse = null;
            _normal = null;
            _specular = null;
            
            Raw = null;
        }
    }

    private class Geometry : PrimitiveData<Vertex>
    {
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
