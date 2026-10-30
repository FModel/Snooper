using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using ImGuiNET;
using Snooper.Core;
using Snooper.Extensions;
using Snooper.Hosting;

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

    private const EBlendMode DefaultBlendMode = EBlendMode.BLEND_Opaque;
    private readonly EBlendMode? _blendMode;
    public EBlendMode BlendMode => _blendMode ?? Parent?.BlendMode ?? DefaultBlendMode;

    private const EMaterialShadingModel DefaultShadingModel = EMaterialShadingModel.MSM_DefaultLit;
    private readonly EMaterialShadingModel? _shadingModel;
    public EMaterialShadingModel ShadingModel => _shadingModel ?? Parent?.ShadingModel ?? DefaultShadingModel;

    private const EMaterialDomain DefaultMaterialDomain = EMaterialDomain.MD_Surface;
    private readonly EMaterialDomain? _materialDomain;
    public EMaterialDomain MaterialDomain => _materialDomain ?? Parent?.MaterialDomain ?? DefaultMaterialDomain;

    private const bool DefaultTwoSided = false;
    private readonly bool? _twoSided;
    public bool TwoSided => _twoSided ?? Parent?.TwoSided ?? DefaultTwoSided;

    private const float DefaultOpacityMaskClipValue = 0.3333f;
    private readonly float? _opacityMaskClipValue;
    public float OpacityMaskClipValue => _opacityMaskClipValue ?? Parent?.OpacityMaskClipValue ?? DefaultOpacityMaskClipValue;

    private ulong? _propertyConnectedMask;
    public ulong? PropertyConnectedMask => _propertyConnectedMask ?? Parent?.PropertyConnectedMask;

    private ShaderParameters? _shaderParameters;
    private ShaderParameters? ActiveParameters => _shaderParameters ?? Parent?.ActiveParameters;

    private readonly Dictionary<string, TextureParameter> _textures = [];
    private readonly List<TextureParameter> _referencedTextures = [];
    private readonly Dictionary<string, float> _scalars = [];
    private readonly Dictionary<string, FLinearColor> _vectors = [];
    private readonly Dictionary<string, bool> _switches = [];

    public MaterialNode(UUnrealMaterial material)
    {
        var provider = material.Owner?.Provider;

        Name = material.Name;
        ProjectName = provider?.ProjectName ?? string.Empty;

        switch (material)
        {
            case UMaterialInstance instance:
            {
                if (instance.Parent is { IsNull: false } && provider is not null)
                    _parent = Parent(instance.Parent.ToSoftObjectPath(), provider);

                if (instance.GetOrDefault<FStructFallback?>("BasePropertyOverrides") is { } overrides)
                {
                    if (overrides.GetOrDefault($"bOverride_{nameof(BlendMode)}", false))
                        _blendMode = overrides.GetOrDefault(nameof(BlendMode), DefaultBlendMode);
                    if (overrides.GetOrDefault($"bOverride_{nameof(ShadingModel)}", false))
                        _shadingModel = overrides.GetOrDefault(nameof(ShadingModel), DefaultShadingModel);
                    if (overrides.GetOrDefault($"bOverride_{nameof(TwoSided)}", false))
                        _twoSided = overrides.GetOrDefault(nameof(TwoSided), DefaultTwoSided);
                    if (overrides.GetOrDefault($"bOverride_{nameof(OpacityMaskClipValue)}", false))
                        _opacityMaskClipValue = overrides.GetOrDefault(nameof(OpacityMaskClipValue), DefaultOpacityMaskClipValue);
                }

                foreach (var parameter in instance.StaticParameters?.StaticSwitchParameters ?? [])
                {
                    _switches[parameter.Name] = parameter.Value;
                }

                if (instance is UMaterialInstanceConstant constant) ReadParameterValues(constant);
                ReadCachedData(instance);
                ReadShaderMaps(instance);
                break;
            }
            case UMaterial root:
            {
                _blendMode = root.GetOrDefault(nameof(BlendMode), DefaultBlendMode);
                _shadingModel = root.GetOrDefault(nameof(ShadingModel), DefaultShadingModel);
                _materialDomain = root.GetOrDefault(nameof(MaterialDomain), DefaultMaterialDomain);
                _twoSided = root.GetOrDefault(nameof(TwoSided), DefaultTwoSided);
                _opacityMaskClipValue = root.GetOrDefault(nameof(OpacityMaskClipValue), DefaultOpacityMaskClipValue);

                for (var i = root.ReferencedTextures.Count - 1; i >= 0; i--)
                {
                    if (root.ReferencedTextures[i] is not { IsNull: false } texture) continue;
                    _referencedTextures.Add(new TextureParameter(texture, provider));
                }

                ReadExpressions(root);
                ReadCachedData(root);
                ReadShaderMaps(root);
                break;
            }
        }

        void ReadParameterValues(UMaterialInstanceConstant constant)
        {
            foreach (var parameter in constant.TextureParameterValues)
            {
                if (parameter.ParameterValue is { } value)
                    _textures[parameter.Name] = new TextureParameter(value, provider);
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
                        _textures.TryAdd(sample.ParameterName.Text, new TextureParameter(texture, provider));
                        break;
                    case UMaterialExpressionTextureBase { Texture: { IsNull: false } texture }:
                        if (!_referencedTextures.Exists(x => x.Name == texture.Name))
                            _referencedTextures.Add(new TextureParameter(texture, provider));
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
                    _referencedTextures.Add(new TextureParameter(index, provider));
            }

            if (cooked.TextureStreamingData.Length > 0)
            {
                var sampled = new HashSet<string>(cooked.TextureStreamingData.Select(x => x.TextureName.Text), StringComparer.OrdinalIgnoreCase);
                var ordered = _referencedTextures.OrderByDescending(x => sampled.Contains(x.Name)).ToList();
                _referencedTextures.Clear();
                _referencedTextures.AddRange(ordered);
            }
        }

        void ReadShaderMaps(UMaterialInterface cooked)
        {
            if (cooked.LoadedMaterialResources is not { Count: > 0 } resources) return;

            var textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scalars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resource in resources)
            {
                if (resource.LoadedShaderMap?.Content is not FMaterialShaderMapContent { MaterialCompilationOutput.UniformExpressionSet: { } uniforms }) continue;

                foreach (var parameters in uniforms.UniformTextureParameters)
                foreach (var parameter in parameters)
                    Add(textures, parameter);

                foreach (var parameter in uniforms.UniformScalarParameters)
                    Add(scalars, parameter);

                foreach (var parameter in uniforms.UniformVectorParameters)
                    Add(vectors, parameter);

                foreach (var parameter in uniforms.UniformNumericParameters)
                {
                    switch (parameter.ParameterType)
                    {
                        case EMaterialParameterType.Scalar:
                            Add(scalars, parameter);
                            break;
                        case EMaterialParameterType.Vector or EMaterialParameterType.DoubleVector:
                            Add(vectors, parameter);
                            break;
                    }
                }
            }

            _shaderParameters = new ShaderParameters(textures, scalars, vectors);

            void Add(HashSet<string> names, object parameter)
            {
                var name = parameter switch
                {
                    FMaterialBaseParameterInfo { ParameterInfo.Name: { IsNone: false } value } => value.Text,
                    FMaterialBaseParameterInfo { ParameterName: not (null or "None") and var value } => value,
                    FMaterialNumericParameterInfo { ParameterInfo.Name: { IsNone: false } value } => value.Text,
                    _ => null
                };

                if (name != null) names.Add(name);
            }
        }

        void ReadParameterDefaults(FStructFallback cached)
        {
            var legacy = cached.TryGetValue(out FStructFallback nested, "Parameters");
            var data = legacy ? nested : cached;
            var infos = legacy ? "ParameterInfos" : "ParameterInfoSet";

            if (data.TryGetValue(out ulong propertyConnectedMask, "PropertyConnectedMask")) _propertyConnectedMask = propertyConnectedMask;
            else if (data.TryGetValue(out uint propertyConnectedBitmask, "PropertyConnectedBitmask")) _propertyConnectedMask = propertyConnectedBitmask;

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

            if (!legacy)
            {
                // Match enum order in FMemoryImageArchive.ReadMaterialParameterType
                var switchEntry = material.Owner?.Provider?.Versions.Game switch
                {
                    >= EGame.GAME_UE5_5 => (int) EMaterialParameterType.StaticSwitch,
                    >= EGame.GAME_UE5_2 => 7,
                    _ => 6
                };

                if (entries.Length > switchEntry && data.TryGetValue(out bool[] switches, "StaticSwitchValues") &&
                    entries[switchEntry].TryGetValue(out FMaterialParameterInfo[] switchInfos, infos))
                {
                    for (var i = 0; i < switchInfos.Length && i < switches.Length; i++)
                        _switches.TryAdd(switchInfos[i].Name.Text, switches[i]);
                }
            }

            var textureEntry = legacy ? 2 : 3;
            if (entries.Length <= textureEntry || !entries[textureEntry].TryGetValue(out FMaterialParameterInfo[] textureInfos, infos)) return;

            if (legacy)
            {
                if (!data.TryGetValue(out FPackageIndex[] indices, "TextureValues")) return;
                for (var i = 0; i < textureInfos.Length && i < indices.Length; i++)
                {
                    if (indices[i] is { } index) _textures.TryAdd(textureInfos[i].Name.Text, new TextureParameter(index, provider));
                }
            }
            else
            {
                if (!data.TryGetValue(out FSoftObjectPath[] paths, "TextureValues")) return;
                for (var i = 0; i < textureInfos.Length && i < paths.Length; i++)
                {
                    _textures.TryAdd(textureInfos[i].Name.Text, new TextureParameter(paths[i], provider));
                }
            }
        }

        // static so the closure holds the path and the provider only, not the constructor's captures (material)
        static Lazy<MaterialNode?> Parent(FSoftObjectPath path, IFileProvider provider) => new(() => MaterialCache.GetNode(path, provider), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsPropertyConnected(EMaterialProperty property) => PropertyConnectedMask is not { } mask || mask == 0 || ((mask >> (int)property) & 0x1) != 0;

    public bool TryGetTexture([MaybeNullWhen(false)] out UTexture texture, params string[] names)
    {
        var active = Active(x => x.Textures);
        for (var node = this; node != null; node = node.Parent)
        {
            if (node._textures.Count == 0) continue;
            foreach (var name in names)
            {
                if (active != null && !active.Contains(name)) continue;
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
        var active = Active(x => x.Textures);
        for (var node = this; node != null; node = node.Parent)
        {
            foreach (var (name, parameter) in node._textures)
            {
                if (active != null && !active.Contains(name)) continue;
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
        if (Active(x => x.Textures) is { } active && !active.Contains(name)) return false;

        for (var node = this; node != null; node = node.Parent)
        {
            if (node._textures.ContainsKey(name)) return true;
        }

        return false;
    }

    public bool TryGetScalar(out float scalar, params string[] names) => TryGet(x => x._scalars, Active(x => x.Scalars), out scalar, names);
    public bool TryGetVector(out FLinearColor vector, params string[] names) => TryGet(x => x._vectors, Active(x => x.Vectors), out vector, names);
    public bool TryGetSwitch(out bool value, params string[] names) => TryGet(x => x._switches, null, out value, names); // static switches pick the shader map, they are never uniforms

    private IReadOnlySet<string>? Active(Func<ShaderParameters, HashSet<string>> kind)
    {
        return ActiveParameters is { } parameters && kind(parameters) is { Count: > 0 } active ? active : null;
    }

    private bool TryGet<T>(Func<MaterialNode, Dictionary<string, T>> values, IReadOnlySet<string>? active, out T value, string[] names) where T : struct
    {
        for (var node = this; node != null; node = node.Parent)
        {
            var own = values(node);
            if (own.Count == 0) continue;

            foreach (var name in names)
            {
                if (active != null && !active.Contains(name)) continue;
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
                TextureGroup.TEXTUREGROUP_Skybox or TextureGroup.TEXTUREGROUP_ImpostorNormalDepth || _occlusionName.IsMatch(parameterName))
            return EMaterialTextureKind.Other;

        if (_diffuseName.IsMatch(parameterName)) return EMaterialTextureKind.Diffuse;
        if (!texture.SRGB)
        {
            if (_normalName.IsMatch(parameterName)) return EMaterialTextureKind.Normal;
            if (_specularName.IsMatch(parameterName)) return EMaterialTextureKind.SpecularMasks;
        }
        if (_emissiveName.IsMatch(parameterName)) return EMaterialTextureKind.Emissive;

        return texture.SRGB ? EMaterialTextureKind.Diffuse : EMaterialTextureKind.Other;
    }

    [GeneratedRegex(@"Occlu|(?:^|_)AO(?:_|\d*$)|(?-i:(?<![A-Z])AO(?![a-z]))", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RegexOcclusion();
    private static readonly Regex _occlusionName = RegexOcclusion();

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

        private TextureParameter(string name, FSoftObjectPath path, IFileProvider? provider)
        {
            Name = name;
            Path = path.ToString();
            Texture = new Lazy<UTexture?>(() => provider is not null && path.TryLoad<UTexture>(provider, out var texture) ? texture : null, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public TextureParameter(FSoftObjectPath path, IFileProvider? provider) : this(path.AssetPathName.Text.SubstringAfterLast('.'), path.WithoutOwner(), provider)
        {

        }

        public TextureParameter(FPackageIndex index, IFileProvider? provider) : this(index.Name, index.ToSoftObjectPath(), provider)
        {

        }
    }

    private ParameterView? _view;
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

        if (_view is not { } view || view.Filter != filter || view.ChainLength != chain.Count)
        {
            _view = view = Build();
        }

        foreach (var (title, open, rows) in view.Kinds)
        {
            if (rows.Count == 0) continue;

            if (!ImGui.TreeNodeEx($"{title} ({rows.Count})###{title}", (open ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None) | ImGuiTreeNodeFlags.SpanAvailWidth)) continue;
            if (!ImGui.BeginTable($"##{title}", 2))
            {
                ImGui.TreePop();
                continue;
            }

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1f);

            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(rows.Count);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        DrawRow(title, rows[i]);
                    }
                }

                clipper.End();
                clipper.Destroy();
            }

            ImGui.EndTable();
            ImGui.TreePop();
        }

        ParameterView Build()
        {
            var parameters = chain.Find(node => node._shaderParameters != null)?._shaderParameters;
            var kinds = new (string Title, bool Open, Func<MaterialNode, IEnumerable<Entry>> Read, IReadOnlySet<string>? Active)[]
            {
                ("Base", true, Base, null),
                ("Scalars", true, node => node._scalars.Select(x => new Entry(x.Key, x.Value.ToString("0.####"))), parameters?.Scalars),
                ("Switches", true, node => node._switches.Select(x => new Entry(x.Key, x.Value.ToString())), null),
                ("Vectors", true, node => node._vectors.Select(x => new Entry(x.Key, $"{x.Value.R:0.###}, {x.Value.G:0.###}, {x.Value.B:0.###}, {x.Value.A:0.###}", Color: x.Value)), parameters?.Vectors),
                ("Textures", true, node => node._textures.Select(x => new Entry(x.Key, x.Value.Name, x.Value.Path)), parameters?.Textures),
                ("Connected", false, Connected, null),
                ("Referenced", false, node => node._referencedTextures.Select(x => new Entry(x.Name, string.Empty, x.Path)), null),
            };

            var built = new (string Title, bool Open, List<Row> Rows)[kinds.Length];
            for (var k = 0; k < kinds.Length; k++)
            {
                var (title, open, read, active) = kinds[k];
                var rows = new List<Row>();
                var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in chain)
                {
                    foreach (var entry in read(node))
                    {
                        if (!index.TryGetValue(entry.Name, out var i))
                        {
                            index[entry.Name] = i = rows.Count;
                            rows.Add(new Row(entry.Name, [], active is { Count: > 0 } && !active.Contains(entry.Name)));
                        }
                        rows[i].Writers.Add((node, entry));
                    }
                }

                if (filter.Length > 0) rows.RemoveAll(row => !row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && !row.Writers.Exists(x => x.Entry.Display.Contains(filter, StringComparison.OrdinalIgnoreCase)));
                built[k] = (title, open, rows);
            }

            return new ParameterView(filter, chain.Count, built);
        }

        void DrawRow(string title, Row row)
        {
            var (name, writers, inactive) = row;
            var entry = writers[0].Entry;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (inactive) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            if (ImGui.Selectable($"{name}##{title}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
            {
                var onName = ImGui.TableGetHoveredColumn() == 0;
                ImGui.SetClipboardText(onName ? name : entry.Path ?? entry.Value);
                Notifications.Push("material.copy", Settings.CopyIcon, onName ? "name copied" : "value copied");
            }

            var replaced = writers.Skip(1).Where(x => x.Entry.Display != entry.Display).DistinctBy(x => x.Entry.Display).ToList();
            if (ImGui.IsItemHovered() && (replaced.Count > 0 || inactive))
            {
                ImGui.BeginTooltip();
                if (inactive) ImGui.TextUnformatted("Not sampled by the shader");
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
            if (inactive) ImGui.PopStyleColor();
        }

        static IEnumerable<Entry> Base(MaterialNode node)
        {
            if (node._blendMode is { } blendMode) yield return new Entry("BlendMode", blendMode.ToString());
            if (node._shadingModel is { } shadingModel) yield return new Entry("ShadingModel", shadingModel.ToString());
            if (node._materialDomain is { } materialDomain) yield return new Entry("MaterialDomain", materialDomain.ToString());
            if (node._twoSided is { } twoSided) yield return new Entry("TwoSided", twoSided.ToString());
        }

        static IEnumerable<Entry> Connected(MaterialNode node)
        {
            if (node._propertyConnectedMask is null or 0) yield break;

            var names = Enum.GetNames<EMaterialProperty>();
            var values = Enum.GetValues<EMaterialProperty>();
            for (var i = 0; i < names.Length; i++)
            {
                if (values[i] >= EMaterialProperty.MP_MAX ||
                    names[i] is nameof(EMaterialProperty.MP_LastCustomizedUVs) or nameof(EMaterialProperty.MP_NumCustomizedUVs)) continue;

                var connected = node.IsPropertyConnected(values[i]);
                if (!connected) continue;

                yield return new Entry(names[i][3..], connected.ToString());
            }
        }
    }

    private readonly record struct Entry(string Name, string Value, string? Path = null, FLinearColor? Color = null)
    {
        public string Display => Value.Length > 0 ? Value : Path ?? string.Empty;
    }
    private readonly record struct Row(string Name, List<(MaterialNode Node, Entry Entry)> Writers, bool Inactive);
    private sealed record ShaderParameters(HashSet<string> Textures, HashSet<string> Scalars, HashSet<string> Vectors);
    private sealed record ParameterView(string Filter, int ChainLength, (string Title, bool Open, List<Row> Rows)[] Kinds);
}
