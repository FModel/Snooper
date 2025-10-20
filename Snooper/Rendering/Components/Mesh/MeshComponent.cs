using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.UObject;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Rendering.Components.Descriptors;
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

public struct PerMaterialMeshData : IPerMaterialData
{
    public bool IsReady { get; init; }
    public uint TextureFlags; // Bit 0: HasDiffuse, Bit 1: HasNormal, Bit 2: HasSpecular, Bit 3: IsTranslucent
    public ulong Diffuse;
    public ulong Normal;
    public ulong Specular;
    public Vector2 Roughness;
    public Vector2 Padding1;
    public Vector3 DiffuseColor;
}

[DefaultActorSystem(typeof(RenderSystem))]
[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent : PrimitiveComponent<Vertex, PerInstanceData, PerMaterialMeshData>
{
    private readonly ResolvedObject?[] _materials;
    
    public sealed override MaterialSection[] Materials { get; }

    protected MeshComponent(ResolvedObject?[] materials, Transform? transform = null, string? name = null) : base(transform, name)
    {
        _materials = materials;
        
        Materials = new MaterialSection[_materials.Length];
    }

    protected MeshComponent(ResolvedObject?[] materials, UMeshComponent component) : base(component)
    {
        _materials = materials;
        
        var overrideMaterials = component.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []);
        for (var i = 0; i < overrideMaterials.Length; i++)
        {
            if (i >= _materials.Length) break;
            if (overrideMaterials[i].IsNull) continue;
                
            _materials[i] = overrideMaterials[i].ResolvedObject;
        }
        
        Materials = new MaterialSection[_materials.Length];
    }

    protected override void OnReworkThis()
    {
        base.OnReworkThis();

        for (var i = 0; i < _materials.Length; i++)
        {
            var index = i;
            Materials[index] = new MaterialSection((uint)index);
            
            // TODO: do somewhere else
            Task.Run(() =>
            {
                if (_materials[index]?.TryLoad(out var m) == true && m is UUnrealMaterial material)
                {
                    var parameters = new CMaterialParams2();
                    material.GetParams(parameters, EMaterialFormat.FirstLayer);

                    Materials[index].MaterialDataContainer = ParseMaterialParameters(parameters, material.Owner.Provider.ProjectName.ToUpperInvariant());
                }
                else
                {
                    Log.Warning("Material at index {MatIndex} is not valid or could not be loaded.", index);
                }
            });
        }
    }
    
    private MaterialDataContainer? ParseMaterialParameters(CMaterialParams2 parameters, string projectName)
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
        
        return new MaterialDataContainer(
            new Texture2D(diffuse),
            normal != null ? new Texture2D(normal) : null,
            specularTex,
            roughness,
            diffuseColor,
            parameters.BlendMode is EBlendMode.BLEND_Translucent or EBlendMode.BLEND_Masked
        );
    }

    private class MaterialDataContainer(Texture diffuse, Texture? normal, Texture? specular, Vector2? roughness = null, Vector3? diffuseColor = null, bool translucent = false) : IMaterialDataContainer
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
            
            uint textureFlags = 1u; // Bit 0: HasDiffuse (always present)
            
            if (_normal != null)
            {
                _normal.Generate();
                _normal.MakeResident();
                textureFlags |= 2u; // Bit 1: HasNormal
            }

            if (_specular != null)
            {
                _specular.Generate();
                _specular.MakeResident();
                textureFlags |= 4u; // Bit 2: HasSpecular
            }
            
            if (IsTranslucent)
            {
                textureFlags |= 8u; // Bit 3: IsTranslucent
            }

            Raw = new PerMaterialMeshData
            {
                IsReady = true,
                TextureFlags = textureFlags,
                Diffuse = _diffuse,
                Normal = _normal ?? 0UL,
                Specular = _specular ?? 0UL,
                Roughness = roughness ?? Vector2.UnitY,
                DiffuseColor = diffuseColor ?? Vector3.One
            };
        }

        public IPerMaterialData? Raw { get; private set; }
        
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

    protected class Geometry : PrimitiveData<Vertex>
    {
        public Geometry(CMeshVertex[] vertices, uint[] indices)
        {
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

            Indices = indices;
        }
    }
}
