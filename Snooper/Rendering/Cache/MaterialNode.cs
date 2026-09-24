using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using ImGuiNET;
using Snooper.Core;
using Snooper.Hosting;
using Snooper.UI;

namespace Snooper.Rendering.Cache;

public enum EMaterialTextureKind
{
    Other,
    Diffuse,
    Normal,
    SpecularMasks,
    Emissive
}

public sealed partial class MaterialNode
{
    public string Name { get; }
    public string ProjectName { get; }

    private readonly Lazy<MaterialNode?>? _parent;
    public MaterialNode? Parent => Bridge.Options.MaterialDepth is not EMaterialDepth.TopLayerOnly && _parent?.Value is { } parent && parent != this ? parent : null;

    private readonly EBlendMode? _blendMode;
    public EBlendMode BlendMode => _blendMode ?? Parent?.BlendMode ?? EBlendMode.BLEND_Opaque;

    private readonly EMaterialShadingModel? _shadingModel;
    public EMaterialShadingModel ShadingModel => _shadingModel ?? Parent?.ShadingModel ?? EMaterialShadingModel.MSM_DefaultLit;

    private readonly bool? _twoSided;
    public bool IsTwoSided => _twoSided ?? Parent?.IsTwoSided ?? false;

    private readonly Dictionary<string, TextureParameter> _textures = [];
    private readonly List<TextureParameter> _referencedTextures = [];
    private readonly Dictionary<string, float> _scalars = [];
    private readonly Dictionary<string, FLinearColor> _vectors = [];
    private readonly Dictionary<string, bool> _switches = [];

    public MaterialNode(UUnrealMaterial material)
    {
        Name = material.Name;
        ProjectName = material.Owner?.Provider?.ProjectName ?? string.Empty;

        switch (material)
        {
            case UMaterialInstance instance:
            {
                if (instance.Parent is { IsNull: false })
                    _parent = new Lazy<MaterialNode?>(() => MaterialCache.GetNode(instance.Parent), LazyThreadSafetyMode.ExecutionAndPublication);

                if (instance.GetOrDefault<FStructFallback?>("BasePropertyOverrides") is { } overrides)
                {
                    if (overrides.GetOrDefault("bOverride_BlendMode", false))
                        _blendMode = overrides.GetOrDefault("BlendMode", EBlendMode.BLEND_Opaque);
                    if (overrides.GetOrDefault("bOverride_ShadingModel", false))
                        _shadingModel = overrides.GetOrDefault("ShadingModel", EMaterialShadingModel.MSM_DefaultLit);
                    if (overrides.GetOrDefault("bOverride_TwoSided", false))
                        _twoSided = overrides.GetOrDefault("TwoSided", false);
                }

                foreach (var parameter in instance.StaticParameters?.StaticSwitchParameters ?? [])
                {
                    _switches[parameter.Name] = parameter.Value;
                }

                if (instance is UMaterialInstanceConstant constant) ReadParameterValues(constant);
                ReadCachedData(instance);
                break;
            }
            case UMaterial root:
            {
                _blendMode = root.GetOrDefault("BlendMode", EBlendMode.BLEND_Opaque);
                _shadingModel = root.GetOrDefault("ShadingModel", EMaterialShadingModel.MSM_DefaultLit);
                _twoSided = root.GetOrDefault("TwoSided", false);

                foreach (var texture in root.ReferencedTextures)
                {
                    if (texture is null or { IsNull: true }) continue;
                    _referencedTextures.Add(new TextureParameter(texture));
                }

                ReadExpressions(root);
                ReadCachedData(root);
                break;
            }
        }

        void ReadParameterValues(UMaterialInstanceConstant constant)
        {
            foreach (var parameter in constant.TextureParameterValues)
            {
                if (parameter.ParameterValue is { } value)
                    _textures[parameter.Name] = new TextureParameter(value);
            }

            foreach (var parameter in constant.ScalarParameterValues)
            {
                _scalars[parameter.Name] = parameter.ParameterValue;
            }

            foreach (var parameter in constant.VectorParameterValues)
            {
                if (parameter.ParameterValue is { } value)
                    _vectors[parameter.Name] = value;
            }
        }

        void ReadExpressions(UMaterial root)
        {
            foreach (var index in root.Expressions)
            {
                if (!index.TryLoad<UMaterialExpression>(out var expression)) continue;

                switch (expression)
                {
                    case UMaterialExpressionTextureSampleParameter { Texture: { IsNull: false } texture } sample:
                        _textures.TryAdd(sample.ParameterName.Text, new TextureParameter(texture));
                        break;
                    case UMaterialExpressionTextureBase { Texture: { IsNull: false } texture }:
                        if (!_referencedTextures.Exists(x => x.Name == texture.Name))
                            _referencedTextures.Add(new TextureParameter(texture));
                        break;
                    case UMaterialExpressionVectorParameter vector:
                        _vectors.TryAdd(vector.ParameterName.Text, vector.DefaultValue);
                        break;
                    case UMaterialExpressionScalarParameter scalar:
                        _scalars.TryAdd(scalar.ParameterName.Text, scalar.DefaultValue);
                        break;
                    case UMaterialExpressionStaticBoolParameter @switch:
                        _switches.TryAdd(@switch.ParameterName.Text, @switch.DefaultValue);
                        break;
                }
            }
        }

        void ReadCachedData(UMaterialInterface cooked)
        {
            if (cooked.CachedExpressionData is { } cached) ReadParameterDefaults(cached);

            foreach (var index in cooked.GetOrDefault<FPackageIndex?[]>("CachedReferencedTextures", []))
            {
                if (index is { IsNull: false } && !_referencedTextures.Exists(x => x.Name == index.Name))
                    _referencedTextures.Add(new TextureParameter(index));
            }

            if (cooked.TextureStreamingData.Length > 0)
            {
                var sampled = new HashSet<string>(cooked.TextureStreamingData.Select(x => x.TextureName.Text), StringComparer.OrdinalIgnoreCase);
                var ordered = _referencedTextures.OrderByDescending(x => sampled.Contains(x.Name)).ToList();
                _referencedTextures.Clear();
                _referencedTextures.AddRange(ordered);
            }
        }

        void ReadParameterDefaults(FStructFallback cached)
        {
            var legacy = cached.TryGetValue(out FStructFallback nested, "Parameters");
            var data = legacy ? nested : cached;
            var infos = legacy ? "ParameterInfos" : "ParameterInfoSet";
            var textureEntry = legacy ? 2 : 3;

            if (!data.TryGetAllValues(out FStructFallback[] entries, "RuntimeEntries")) return;

            if (entries.Length > 0 && data.TryGetValue(out float[] scalars, "ScalarValues") &&
                entries[0].TryGetValue(out FMaterialParameterInfo[] scalarInfos, infos))
            {
                for (var i = 0; i < scalarInfos.Length && i < scalars.Length; i++)
                    _scalars.TryAdd(scalarInfos[i].Name.Text, scalars[i]);
            }

            if (entries.Length > 1 && data.TryGetValue(out FLinearColor[] vectors, "VectorValues") &&
                entries[1].TryGetValue(out FMaterialParameterInfo[] vectorInfos, infos))
            {
                for (var i = 0; i < vectorInfos.Length && i < vectors.Length; i++)
                    _vectors.TryAdd(vectorInfos[i].Name.Text, vectors[i]);
            }

            if (entries.Length <= textureEntry || !entries[textureEntry].TryGetValue(out FMaterialParameterInfo[] textureInfos, infos)) return;

            if (legacy)
            {
                if (!data.TryGetValue(out FPackageIndex[] indices, "TextureValues")) return;
                for (var i = 0; i < textureInfos.Length && i < indices.Length; i++)
                {
                    if (indices[i] is { } index) _textures.TryAdd(textureInfos[i].Name.Text, new TextureParameter(index));
                }
            }
            else
            {
                if (!data.TryGetValue(out FSoftObjectPath[] paths, "TextureValues")) return;
                for (var i = 0; i < textureInfos.Length && i < paths.Length; i++)
                {
                    _textures.TryAdd(textureInfos[i].Name.Text, new TextureParameter(paths[i]));
                }
            }
        }
    }

    public bool TryGetTexture([MaybeNullWhen(false)] out UTexture texture, params string[] names)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            foreach (var name in names)
            {
                if (node._textures.TryGetValue(name, out var parameter) && parameter.Texture.Value is { } found)
                {
                    texture = found;
                    return true;
                }
            }
        }

        texture = null;
        return false;
    }
    public bool TryGetTexture([MaybeNullWhen(false)] out UTexture texture, EMaterialTextureKind kind)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            foreach (var (name, parameter) in node._textures)
            {
                if (parameter.Texture.Value is UTexture2D found && Classify(name, found) == kind)
                {
                    texture = found;
                    return true;
                }
            }

            if (Bridge.Options.MaterialDepth is EMaterialDepth.AllLayersNoRef) continue;

            foreach (var referenced in node._referencedTextures)
            {
                if (referenced.Texture.Value is UTexture2D found && Classify(referenced.Name, found) == kind)
                {
                    texture = found;
                    return true;
                }
            }
        }

        texture = null;
        return false;
    }
    public bool HasTexture(string name)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            if (node._textures.ContainsKey(name)) return true;
        }

        return false;
    }

    public bool TryGetScalar(out float scalar, params string[] names) => TryGet(x => x._scalars, out scalar, names);
    public bool TryGetVector(out FLinearColor vector, params string[] names) => TryGet(x => x._vectors, out vector, names);
    public bool TryGetSwitch(out bool value, params string[] names) => TryGet(x => x._switches, out value, names);

    private bool TryGet<T>(Func<MaterialNode, Dictionary<string, T>> values, out T value, string[] names) where T : struct
    {
        for (var node = this; node != null; node = node.Parent)
        {
            var own = values(node);
            foreach (var name in names)
            {
                if (own.TryGetValue(name, out value)) return true;
            }
        }

        value = default;
        return false;
    }

    private EMaterialTextureKind Classify(string parameterName, UTexture texture)
    {
        if (texture.CompressionSettings is TextureCompressionSettings.TC_Normalmap || texture.LODGroup is
                TextureGroup.TEXTUREGROUP_WorldNormalMap or TextureGroup.TEXTUREGROUP_CharacterNormalMap or
                TextureGroup.TEXTUREGROUP_WeaponNormalMap or TextureGroup.TEXTUREGROUP_VehicleNormalMap)
            return EMaterialTextureKind.Normal;

        if (texture.CompressionSettings is TextureCompressionSettings.TC_Masks || texture.LODGroup is
                TextureGroup.TEXTUREGROUP_WorldSpecular or TextureGroup.TEXTUREGROUP_CharacterSpecular or
                TextureGroup.TEXTUREGROUP_WeaponSpecular or TextureGroup.TEXTUREGROUP_VehicleSpecular)
            return EMaterialTextureKind.SpecularMasks;

        if (texture.CompressionSettings is not (TextureCompressionSettings.TC_Default or TextureCompressionSettings.TC_BC7 or TextureCompressionSettings.TC_LQ) ||
            texture.LODGroup is TextureGroup.TEXTUREGROUP_UI or TextureGroup.TEXTUREGROUP_Lightmap or TextureGroup.TEXTUREGROUP_Shadowmap or
                TextureGroup.TEXTUREGROUP_Skybox or TextureGroup.TEXTUREGROUP_ImpostorNormalDepth)
            return EMaterialTextureKind.Other;

        if (_diffuseName.IsMatch(parameterName)) return EMaterialTextureKind.Diffuse;
        if (_normalName.IsMatch(parameterName)) return EMaterialTextureKind.Normal;
        if (_specularName.IsMatch(parameterName)) return EMaterialTextureKind.SpecularMasks;
        if (_emissiveName.IsMatch(parameterName)) return EMaterialTextureKind.Emissive;

        return texture.SRGB ? EMaterialTextureKind.Diffuse : EMaterialTextureKind.Other;
    }

    [GeneratedRegex(CMaterialParams2.RegexDiffuse, RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexDiffuse();
    private static readonly Regex _diffuseName = RegexDiffuse();

    [GeneratedRegex(CMaterialParams2.RegexNormals, RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexNormals();
    private static readonly Regex _normalName = RegexNormals();

    [GeneratedRegex(CMaterialParams2.RegexSpecularMasks, RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexSpecularMasks();
    private static readonly Regex _specularName = RegexSpecularMasks();

    [GeneratedRegex(CMaterialParams2.RegexEmissive, RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexEmissive();
    private static readonly Regex _emissiveName = RegexEmissive();

    private sealed class TextureParameter
    {
        public readonly string Name;
        public readonly string Path;
        public readonly Lazy<UTexture?> Texture;

        private TextureParameter(string name, string path, Func<UTexture?> load)
        {
            Name = name;
            Path = path;
            Texture = new Lazy<UTexture?>(load, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public TextureParameter(FSoftObjectPath path) : this(path.AssetPathName.Text.SubstringAfterLast('.'), path.ToString(), () => path.TryLoad<UTexture>(out var texture) ? texture : null)
        {

        }

        public TextureParameter(FPackageIndex index) : this(index.Name, index.ResolvedObject?.GetPathName() ?? index.Name, () => index.TryLoad<UTexture>(out var texture) ? texture : null)
        {

        }
    }

    internal void DrawControls(string filter)
    {
        var chain = new List<MaterialNode>();
        for (var node = this; node != null; node = node._parent is { IsValueCreated: true, Value: { } parent } && parent != node ? parent : null)
        {
            chain.Add(node);
        }

        if (chain[^1]._parent is { IsValueCreated: false })
        {
            ImGui.TextColored(Settings.OrangeColor, $"{Settings.TriangleExclamationIcon}  Parent not parsed, what it sets is missing here");
        }

        var kinds = new (string Title, bool Open, Func<MaterialNode, IEnumerable<Entry>> Read)[]
        {
            ("Base", true, Base),
            ("Scalars", true, node => node._scalars.Select(x => new Entry(x.Key, x.Value.ToString("0.####")))),
            ("Switches", true, node => node._switches.Select(x => new Entry(x.Key, x.Value.ToString()))),
            ("Vectors", true, node => node._vectors.Select(x => new Entry(x.Key, $"{x.Value.R:0.###}, {x.Value.G:0.###}, {x.Value.B:0.###}, {x.Value.A:0.###}", Color: x.Value))),
            ("Textures", true, node => node._textures.Select(x => new Entry(x.Key, x.Value.Name, x.Value.Path))),
            ("Referenced", false, node => node._referencedTextures.Select(x => new Entry(x.Name, string.Empty, x.Path))),
        };

        foreach (var (title, open, read) in kinds)
        {
            var rows = new List<(string Name, List<(MaterialNode Node, Entry Entry)> Writers)>();
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in chain)
            {
                foreach (var entry in read(node))
                {
                    if (!index.TryGetValue(entry.Name, out var i))
                    {
                        index[entry.Name] = i = rows.Count;
                        rows.Add((entry.Name, []));
                    }
                    rows[i].Writers.Add((node, entry));
                }
            }

            if (filter.Length > 0) rows.RemoveAll(row => !row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && !row.Writers.Exists(x => x.Entry.Display.Contains(filter, StringComparison.OrdinalIgnoreCase)));
            if (rows.Count == 0) continue;

            if (!ImGui.TreeNodeEx($"{title} ({rows.Count})##{title}", (open ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None) | ImGuiTreeNodeFlags.SpanAvailWidth)) continue;
            if (!ImGui.BeginTable($"##{title}", 2))
            {
                ImGui.TreePop();
                continue;
            }

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1f);

            foreach (var (name, writers) in rows)
            {
                var entry = writers[0].Entry;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{name}##{title}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
                {
                    ImGui.SetClipboardText(entry.Path ?? entry.Value);
                    Notifications.Push("material.copy", Settings.CopyIcon, "value copied");
                }

                var replaced = writers.Skip(1).Where(x => x.Entry.Display != entry.Display).DistinctBy(x => x.Entry.Display).ToList();
                if (ImGui.IsItemHovered() && replaced.Count > 0)
                {
                    ImGui.BeginTooltip();
                    foreach (var (node, value) in replaced)
                    {
                        ImGui.TextDisabled($"Parent {chain.IndexOf(node)}");
                        ImGui.SameLine();
                        if (value.Color is { } replacedColor)
                        {
                            ImGui.ColorButton($"##{title}{name}{node.Name}", new Vector4(replacedColor.R, replacedColor.G, replacedColor.B, replacedColor.A), ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(ImGui.GetTextLineHeight()));
                            ImGui.SameLine();
                        }
                        ImGui.TextUnformatted(value.Display);
                    }
                    ImGui.EndTooltip();
                }

                if (replaced.Count > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"+{replaced.Count}");
                }

                ImGui.TableNextColumn();
                if (entry.Color is { } c)
                {
                    ImGui.ColorButton($"##{title}{name}", new Vector4(c.R, c.G, c.B, c.A), ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                }
                ImGui.TextUnformatted(entry.Value);
            }

            ImGui.EndTable();
            ImGui.TreePop();
        }


        static IEnumerable<Entry> Base(MaterialNode node)
        {
            if (node._blendMode is { } blendMode) yield return new Entry("BlendMode", blendMode.ToString());
            if (node._shadingModel is { } shadingModel) yield return new Entry("ShadingModel", shadingModel.ToString());
            if (node._twoSided is { } twoSided) yield return new Entry("TwoSided", twoSided.ToString());
        }
    }

    private readonly record struct Entry(string Name, string Value, string? Path = null, FLinearColor? Color = null)
    {
        public string Display => Value.Length > 0 ? Value : Path ?? string.Empty;
    }
}
