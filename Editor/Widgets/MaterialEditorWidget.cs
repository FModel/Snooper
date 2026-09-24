using CUE4Parse.UE4.Assets.Exports.Material;
using Editor.Managers;
using Editor.Modals;
using ImGuiNET;
using Snooper;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.UI;

namespace Editor.Widgets;

/// <summary>
/// Full editing surface for every material of the selected mesh, one collapsing header each, the same
/// way components present themselves in the inspector.
/// Every edit goes through <see cref="MaterialSection.BeginEdit"/>, so the shared cache entry is never
/// mutated — the section gets a private clone the first time anything here changes.
/// </summary>
public class MaterialEditorWidget : PanelWidget
{
    public override string PanelTitle => Settings.MaterialEditorWindow;
    public override PanelGroup Group => PanelGroup.Tools;

    public override bool IsOpen { get; set; }

    private static readonly EBlendMode[] _blendModes = Enum.GetValues<EBlendMode>().Distinct().ToArray();
    private static readonly EMaterialShadingModel[] _shadingModels = Enum.GetValues<EMaterialShadingModel>().Distinct().ToArray();
    private static readonly string[] _blendModeLabels = _blendModes.Select(static mode => mode.GetDescription()).ToArray();
    private static readonly string[] _shadingModelLabels = _shadingModels.Select(static mode => mode.GetDescription()).ToArray();

    private readonly Dictionary<int, HeaderButtons> _headerButtons = [];

    private int _lastComponentId = -1;
    private int _lastSelectedSectionId = -1;
    private bool _edited;
    private string _parameterSearch = string.Empty;

    public void Reset()
    {
        _lastComponentId = -1;
        _lastSelectedSectionId = -1;
        _headerButtons.Clear();
    }

    protected override void DrawContents(EditorManager editor)
    {
        var component = editor.SelectedComponent ?? editor.SelectedActor?.RootComponent;
        if (component is not IPrimitiveComponent primitive)
        {
            ImGui.TextDisabled("No mesh selected.");
            return;
        }

        if (component.Id != _lastComponentId)
        {
            _lastComponentId = component.Id;
            _headerButtons.Clear();
        }

        var materials = primitive.Materials;
        if (materials.Length == 0)
        {
            ImGui.TextDisabled("This mesh has no materials.");
            return;
        }

        ImGui.SeparatorText($"{component.Name} ({materials.Length} Material{(materials.Length != 1 ? "s" : "")})");

        var selected = primitive.SelectedMaterial;
        var selectionChanged = selected != null && selected.SectionId != _lastSelectedSectionId;
        _lastSelectedSectionId = selected?.SectionId ?? -1;

        for (var slot = 0; slot < materials.Length; slot++)
        {
            if (materials[slot] is not { } section) continue;
            DrawMaterial(primitive, section, slot, selectionChanged, section.SectionId == selected?.SectionId);
        }
    }

    private void DrawMaterial(IPrimitiveComponent primitive, MaterialSection section, int slot, bool selectionChanged, bool isSelected)
    {
        if (selectionChanged) ImGui.SetNextItemOpen(isSelected, ImGuiCond.Always);
        MaterialCache.TryGetNode(section.CacheKey, out var parsed);
        var open = ImGui.CollapsingHeader($"{slot}: {section.MaterialDataContainer?.Name ?? parsed?.Name ?? Settings.NoName}###Material{slot}", ImGuiTreeNodeFlags.AllowOverlap);
        GetHeaderButtons(primitive, section, slot).Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (selectionChanged && isSelected) ImGui.SetScrollHereY(0.5f);
        if (!open) return;

        ImGui.PushID(section.SectionId);
        ImGui.Indent();
        var container = section.MaterialDataContainer;
        if (container is null && parsed is null)
        {
            ImGui.TextDisabled("This section has no material.");
        }
        else if (ImGui.BeginTabBar("##MaterialTabs"))
        {
            DrawContainerTabs(section, container);

            if (parsed is not null && ImGui.BeginTabItem("Parameters##ParametersTab"))
            {
                DrawParameters(parsed);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
        ImGui.Unindent();
        ImGui.PopID();
    }

    private HeaderButtons GetHeaderButtons(IPrimitiveComponent primitive, MaterialSection section, int slot)
    {
        if (_headerButtons.TryGetValue(section.SectionId, out var existing)) return existing;

        var buttons = new HeaderButtons($"Material{slot}")
            .Add(() => section.IsVisible ? Settings.EyeIcon : Settings.EyeSlashIcon, () => "Toggle Visibility\nHide every section drawn with this material",
                () => primitive.SetMaterialVisibility(section.Index, !section.IsVisible), null,
                () => section.IsVisible ? null : Settings.RedColor)
            .Add(() => Settings.RightLeftIcon, () => "Swap\nPick another material the scene has already loaded",
                () => MaterialPickerModal.Instance.Open(entry => SwapMaterial(section, entry.Key)),
                () => section.MaterialDataContainer is MaterialDataContainer)
            .Add(() => Settings.ArrowRotateLeftIcon, () => "Reset\nReset the material and parameters this section was loaded with",
                section.RevertEdit,
                () => section.IsEdited,
                () => section.IsEdited ? Settings.OrangeColor : null);

        _headerButtons[section.SectionId] = buttons;
        return buttons;
    }

    private void DrawContainerTabs(MaterialSection section, IMaterialDataContainer? container)
    {
        if (container is MaterialDataContainer { IsGpuDataReady: true } material)
        {
            _edited = false;
            for (var i = 0; i < material.LayerCount; i++)
            {
                if (!ImGui.BeginTabItem($"Layer {i}##LayerTab{i}")) continue;

                DrawLayer(section, material, i);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Global##GlobalTab"))
            {
                DrawGlobalProperties(section, material);
                ImGui.EndTabItem();
            }

            if (_edited) section.CommitEdit();
            return;
        }

        // nothing to edit yet, or ever
        if (!ImGui.BeginTabItem("Material##MaterialTab")) return;

        switch (container)
        {
            case null:
                ImGui.TextColored(Settings.OrangeColor, "Could not be set up: no diffuse texture, color or normal map was found.");
                break;
            case MaterialDataContainer:
                ImGui.TextColored(Settings.OrangeColor, "Uploading textures...");
                break;
            default:
                ImGui.TextDisabled($"{container.Name} is not an editable material type.");
                break;
        }

        ImGui.EndTabItem();
    }

    private void DrawParameters(MaterialNode node)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##MaterialParameterFilter", $"{Settings.MagnifyingGlassIcon}  Filter by name or value", ref _parameterSearch, 128, ImGuiInputTextFlags.AutoSelectAll);

        node.DrawControls(_parameterSearch);
    }

    private void DrawLayer(MaterialSection section, MaterialDataContainer material, int layerIndex)
    {
        var layer = material.Layers[layerIndex];

        foreach (var slot in Enum.GetValues<MaterialTextureSlot>())
        {
            DrawTextureSlot(section, layerIndex, slot, material.GetSlotTexture(layerIndex, slot));
        }

        EditorUI.PropertyValueTable($"MaterialLayer{layerIndex}", () =>
        {
            var diffuseColor = layer.DiffuseColor;
            if (EditorUI.ColorEdit3("Diffuse Color", ref diffuseColor, ImGuiColorEditFlags.Float))
            {
                Edit(section, edited => edited.Layers[layerIndex].DiffuseColor = diffuseColor);
            }

            var roughness = layer.Roughness;
            var minChanged = EditorUI.SliderFloat("Roughness Min", ref roughness.X, 0.0f, 1.0f, "%.3f");
            var maxChanged = EditorUI.SliderFloat("Roughness Max", ref roughness.Y, 0.0f, 1.0f, "%.3f");
            if (minChanged || maxChanged)
            {
                Edit(section, edited => edited.Layers[layerIndex].Roughness = roughness);
            }
        }, false);
    }

    private void DrawTextureSlot(MaterialSection section, int layerIndex, MaterialTextureSlot slot, Texture? texture)
    {
        ImGui.PushID($"{layerIndex}_{slot}");

        EditorUI.DrawThumbnail(texture, slot.ToString()[..1], 3);
        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.TextUnformatted(slot.ToString());

        EditorUI.Caption(texture is null ? "None" : $"{texture.Name}\n{texture.Width}x{texture.Height}, {texture.FormatName}, {texture.GetFormattedSpace()}");

        if (EditorUI.IconButton(Settings.ImageIcon, $"Swap\nPick another {slot} texture the scene has already loaded"))
        {
            TexturePickerModal.Instance.Open(picked => SetTexture(section, layerIndex, slot, picked));
        }

        if (texture is not null && slot != MaterialTextureSlot.Diffuse)
        {
            ImGui.SameLine();
            if (EditorUI.IconButton(Settings.TrashIcon, $"Clear\nRemove the {slot} texture from this layer", textColor: Settings.RedColor))
            {
                SetTexture(section, layerIndex, slot, null);
            }
        }
        ImGui.EndGroup();

        ImGui.PopID();
        ImGui.Spacing();
    }

    private void DrawGlobalProperties(MaterialSection section, MaterialDataContainer material)
    {
        EditorUI.PropertyValueTable("MaterialGlobals", () =>
        {
            ImGui.BeginDisabled();
            EditorUI.Property("Blend Mode");
            var blendIndex = (uint) Math.Max(0, Array.IndexOf(_blendModes, material.BlendMode));
            if (EditorUI.LabelCombo("##BlendMode", ref blendIndex, _blendModeLabels))
            {
                Edit(section, edited => edited.BlendMode = _blendModes[blendIndex]);
            }
            EditorUI.Property("Shading Model");
            var shadingIndex = (uint) Math.Max(0, Array.IndexOf(_shadingModels, material.ShadingModel));
            if (EditorUI.LabelCombo("##ShadingModel", ref shadingIndex, _shadingModelLabels))
            {
                Edit(section, edited => edited.ShadingModel = _shadingModels[shadingIndex]);
            }
            ImGui.EndDisabled();

            EditorUI.Text("Translucent", material.IsTranslucent ? "\uf00c" : "\uf00d");
            EditorUI.Text("Layers", material.LayerCount.ToString());

            EditorUI.Property("GPU Status");
            if (material.IsGpuDataReady) ImGui.TextColored(Settings.GreenColor, "Ready");
            else ImGui.TextColored(Settings.OrangeColor, "Uploading...");
        }, false);
    }

    private void SetTexture(MaterialSection section, int layerIndex, MaterialTextureSlot slot, Texture? texture)
    {
        BindlessTexture? bindless = null;
        if (texture is not null && !TextureCache.TryGetBindless(texture.Guid, out bindless))
        {
            Notifications.Push("material.texture", Settings.ImageIcon, $"{texture.Name} is no longer resident");
            return;
        }

        if (section.BeginEdit() is not { } edited) return;

        edited.SetLayerTexture(layerIndex, slot, texture, bindless);
        section.CommitEdit();
    }

    private void SwapMaterial(MaterialSection section, string cacheKey)
    {
        var wasTranslucent = section.IsTranslucent;
        section.SwapMaterial(cacheKey);

        if (section.IsTranslucent == wasTranslucent) return;

        Notifications.Push("material.opacity",
            Settings.PaletteIcon,
            "Opacity changed - the draw stays in its original pass until the mesh is reloaded");
    }

    private void Edit(MaterialSection section, Action<MaterialDataContainer> mutate)
    {
        if (section.BeginEdit() is not { } edited) return;

        mutate(edited);
        _edited = true;
    }
}
