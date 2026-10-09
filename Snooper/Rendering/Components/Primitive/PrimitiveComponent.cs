using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.FastGeoStreaming;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Primitive;

public interface IPrimitiveComponent
{
    public MaterialSection[] Materials { get; }
    public void SetMaterialVisibility(uint materialIndex, bool visible);
    internal MaterialSection? SelectedMaterial { get; }
}

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> : SpatialComponent, IPrimitiveComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.InstanceData | DirtyFlags.Visibility | DirtyFlags.ManualLodSwap | DirtyFlags.Opacity | DirtyFlags.Outline;

    public PrimitiveDescriptor<TVertex> Descriptor
    {
        get => field ?? throw new InvalidOperationException($"Descriptor not initialized for {Name} of type {GetType().Name}.");
        protected init;
    }

    public ResourcesMetadata? Metadata { get; internal set; }

    public abstract MaterialSection[] Materials { get; }

    private bool? _isOpaque;
    public bool IsOpaque
    {
        get => _isOpaque ??= SupportsOpaquePass;
        internal set
        {
            if (!SupportsOpaquePass || _isOpaque == value) return;

            _isOpaque = value;
            MarkDirty(DirtyFlags.Opacity);
        }
    }

    public sealed override bool IsVisible
    {
        get => base.IsVisible;
        protected set
        {
            if (base.IsVisible == value) return;

            base.IsVisible = value;
            SetMaterialsVisible(value);
        }
    }

    public readonly bool CastShadow = true;
    public Vector2 DrawDistance { get; protected init; } = Vector2.Zero;

    /// <summary>
    /// opaque pass requires shader support for writing to multiple render targets, so by default it's disabled and primitives are rendered in the translucent pass
    /// </summary>
    protected virtual bool SupportsOpaquePass => false;

    protected PrimitiveComponent(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> other) : base(other)
    {
        if (other.Descriptor != null)
        {
            Descriptor = (PrimitiveDescriptor<TVertex>) other.Descriptor.Clone();
        }
        IsOpaque = other.IsOpaque;
        IsVisible = other.IsVisible;
        CastShadow = other.CastShadow;
    }

    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        CastShadow = component.CastShadow && component.bCastDynamicShadow;

        if (component.TryGetValue(out float minDrawDistance, "MinDrawDistance"))
        {
            DrawDistance = DrawDistance with { X = minDrawDistance * Settings.GlobalScale };
        }
        if (component.TryGetValue(out float maxDrawDistance, "CachedMaxDrawDistance"))
        {
            DrawDistance = DrawDistance with { Y = maxDrawDistance * Settings.GlobalScale };
        }
    }

    protected PrimitiveComponent(FFastGeoPrimitiveComponent component) : base(component)
    {
        var desc = component.SceneProxyDesc.PrimitiveSceneProxyDesc;
        IsVisible = component.bVisible || !desc.bIsHidden;
        CastShadow = desc.CastShadow && desc.bCastDynamicShadow;
        DrawDistance = new Vector2(desc.MinDrawDistance, desc.CachedMaxDrawDistance) * Settings.GlobalScale;
    }

    protected PrimitiveComponent(USceneComponent component) : base(component)
    {

    }

    private TInstanceData[]? _cachedInstanceData;
    public TInstanceData[] GetPerInstanceData()
    {
        var matrices = GetWorldMatrices();
        var data = new TInstanceData[matrices.Length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = new TInstanceData { Matrix = matrices[i] };
        }

        if (_cachedInstanceData is null)
        {
            if (ApplyInstanceData(data))
                _cachedInstanceData = data;
        }
        else
        {
            CopyCachedData(data, _cachedInstanceData);
        }

        return data;
    }
    protected virtual bool ApplyInstanceData(TInstanceData[] data)
    {
        return false;
    }
    protected virtual void CopyCachedData(TInstanceData[] data, TInstanceData[] cached)
    {

    }

    protected override (Vector3, float) GetTeleportPosition(CameraComponent camera, Quaternion rotation)
    {
        var upLimit = MathF.Tan(camera.FieldOfViewRadians / 2f);
        var sideLimit = upLimit * camera.AspectRatio;

        var forward = Vector3.Transform(Settings.ForwardVector, rotation);
        var right = Vector3.Transform(Settings.RightVector, rotation);
        var up = Vector3.Transform(Settings.UpVector, rotation);

        var center = Vector3.Transform(Descriptor.Bounds.Center, GizmoMatrix);

        var distance = 0f;
        for (var i = 0; i < 8; i++)
        {
            var offset = Vector3.Transform(GetBoundsCorner(i), GizmoMatrix) - center;
            var depth = Vector3.Dot(offset, -forward);

            distance = MathF.Max(distance, MathF.Abs(Vector3.Dot(offset, right)) / sideLimit - depth);
            distance = MathF.Max(distance, MathF.Abs(Vector3.Dot(offset, up)) / upLimit - depth);
        }

        return (center, MathF.Max(distance, 0.1f));
    }

    private Vector3 GetBoundsCorner(int index)
    {
        var extents = Descriptor.Bounds.Extents;

        return Descriptor.Bounds.Center + new Vector3(
            (index & 1) == 0 ? -extents.X : extents.X,
            (index & 2) == 0 ? -extents.Y : extents.Y,
            (index & 4) == 0 ? -extents.Z : extents.Z);
    }

    public bool SnapToGround()
    {
        var lowest = float.MaxValue;
        for (var i = 0; i < 8; i++)
        {
            lowest = MathF.Min(lowest, Vector3.Transform(GetBoundsCorner(i), WorldMatrix).Y);
        }

        if (lowest >= -0.001f || !Matrix4x4.Invert(GetRelationMatrix(), out var invRelation))
            return false;

        var transform = (Transform) GetLocalTransform().Clone();
        transform.Position -= Vector3.TransformNormal(new Vector3(0f, lowest, 0f), invRelation);
        SetLocalTransform(transform);
        return true;
    }

    public bool IsMaterialVisible(uint materialIndex) => IsActorVisibleRecursive && (materialIndex >= Materials.Length || Materials[materialIndex] is not { IsVisible: false });
    public void SetMaterialVisibility(uint materialIndex, bool visible)
    {
        if (materialIndex >= Materials.Length || Materials[materialIndex] is not { } material) return;
        if (material.IsVisible == visible) return;

        material.IsVisible = visible;
        MarkVisibilityDirty();
    }
    protected void SetMaterialsVisible(bool visible)
    {
        if (Materials is { } materials)
        {
            foreach (var material in materials)
            {
                material?.IsVisible = visible;
            }
        }

        MarkVisibilityDirty();
    }

    private void MarkVisibilityDirty() => MarkDirty(DirtyFlags.Visibility);
    internal override void OnActorVisibilityChanged() => MarkVisibilityDirty();

    public override string Icon => "\ue4e2";

    protected internal override string VisibilityIcon => HasVisibleMaterial ? HasHiddenMaterial ? Settings.EyeLowVisionIcon : Settings.EyeIcon : Settings.EyeSlashIcon;
    protected internal override Vector4? VisibilityColor => HasVisibleMaterial ? HasHiddenMaterial ? Settings.OrangeColor : null : Settings.RedColor;

    private const string HeaderLabel = "Mesh";
    private HeaderButtons HeaderButtons => field ??= new HeaderButtons(HeaderLabel)
        .Add(Settings.CopyIcon, "Copy Path", () => ImGui.SetClipboardText(Descriptor.Path));

    private const string MaterialsLabel = "Materials";
    private HeaderButtons MaterialsButtons => field ??= new HeaderButtons(MaterialsLabel)
        .Add(Settings.PaletteIcon, "Material Inspector", () => WindowRequests.Request(Settings.MaterialInspectorWindow))
        .Add(() => VisibilityIcon, () => HasVisibleMaterial ? "Hide All" : "Show All", () => SetMaterialsVisible(!HasVisibleMaterial), null, () => VisibilityColor);

    private PropertyToggleButton[] MaterialButtons => field ??=
    [
        new PropertyToggleButton(
            () => SelectedMaterial is { IsVisible: false } ? Settings.EyeSlashIcon : Settings.EyeIcon,
            () => SetMaterialVisibility((uint) _materialIndex, SelectedMaterial is { IsVisible: false }),
            () => SelectedMaterial is { IsVisible: false } ? "Show" : "Hide",
            textColor: () => SelectedMaterial is { IsVisible: false } ? Settings.RedColor : null),
        new PropertyToggleButton(
            () => Settings.CopyIcon,
            () => ImGui.SetClipboardText(SelectedMaterial is { Path: { } path } ? Actor?.ActorManager?.FileProvider.FixPath(path) ?? path : SelectedMaterial?.Name),
            () => "Copy Path")
    ];

    private PropertyToggleButton[] LayerButtons => field ??=
    [
        new PropertyToggleButton(
            () => Settings.AngleLeftIcon,
            () => { _materialLayerIndex = _materialLayerIndex <= 0 ? MaterialLayerCount - 1 : _materialLayerIndex - 1; },
            () => "Previous Layer",
            visible: () => MaterialLayerCount > 1),
        new PropertyToggleButton(
            () => Settings.AngleRightIcon,
            () => { _materialLayerIndex = _materialLayerIndex >= MaterialLayerCount - 1 ? 0 : _materialLayerIndex + 1; },
            () => "Next Layer",
            visible: () => MaterialLayerCount > 1)
    ];

    protected int DisplayedLod => Math.Clamp(Metadata?.GeometryHandle.OverrideLod ?? 0, 0, Descriptor.Lods.Length - 1);

    private int _materialIndex;
    private int _materialLayerIndex;
    private int MaterialLayerCount => SelectedMaterial?.MaterialDataContainer is MaterialDataContainer material ? material.LayerCount : 0;
    public MaterialSection? SelectedMaterial => _materialIndex >= 0 && _materialIndex < Materials.Length ? Materials[_materialIndex] : null;

    private bool HasHiddenMaterial => Array.Exists(Materials, x => x is { IsVisible: false });
    private bool HasVisibleMaterial => Array.Exists(Materials, x => x is not { IsVisible: false });

    public override void DrawControls()
    {
        base.DrawControls();
        if (string.IsNullOrEmpty(Descriptor.Name))
            return;

        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (open) EditorUI.PropertyValueTable(HeaderLabel, DrawMesh);
        DrawMaterials();

        void DrawMesh()
        {
            EditorUI.Text("Name", Descriptor.Name);
            EditorUI.Text("Bounds", Descriptor.Bounds.BoundsFormatted);
            EditorUI.Text("Shadow", CastShadow ? "Cast" : "None");
            if (DrawDistance != Vector2.Zero)
            {
                EditorUI.Text("Draw Distance", $"Min: {DrawDistance.X}, Max: {DrawDistance.Y}");
            }

            EditorUI.Property($"LODs ({Descriptor.Lods.Length})");
            ImGui.BeginGroup();
            var maxLod = Descriptor.Lods.Length - 1;
            var minLod = maxLod == 0 ? 0 : -1;
            var value = Metadata == null ? minLod : Metadata.GeometryHandle.OverrideLod;

            ImGui.BeginDisabled(minLod == maxLod);
            var slided1 = ImGui.SliderInt("##LODSlider", ref value, minLod, maxLod);
            ImGui.EndDisabled();
            if (slided1)
            {
                if (Metadata != null && IsVisible && maxLod > 0)
                {
                    Metadata.GeometryHandle.OverrideLod = value;
                    MarkDirty(DirtyFlags.ManualLodSwap);
                }
            }

            var lod = Descriptor.Lods[Math.Max(0, value)];
            switch (value)
            {
                case -1:
                    EditorUI.Caption("Auto (Screen Size Based)");
                    break;
                case >= 0 when value < Descriptor.Lods.Length:
                    EditorUI.Caption($"{lod.VertexCount} Vertices, {lod.IndexCount} Indices, {lod.ScreenSize * 100f:0.#}% Screen Size");
                    break;
            }

            ImGui.Spacing();
            ImGui.EndGroup();
        }

        void DrawMaterials()
        {
            var materials = Materials;
            if (materials.Length == 0) return;

            var expanded = ImGui.CollapsingHeader(MaterialsLabel,ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
            MaterialsButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());
            if (!expanded) return;

            var style = ImGui.GetStyle();
            var frame = ImGui.GetFrameHeight();
            var sections = Descriptor.Lods[DisplayedLod].Sections;

            ImGui.Indent();
            if (ImGui.BeginTabBar("##MaterialSlots", ImGuiTabBarFlags.FittingPolicyScroll | ImGuiTabBarFlags.DrawSelectedOverline))
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] is not { } material) continue;

                    // a slot the displayed lod does not draw with is dimmed
                    var used = false;
                    for (var s = 0; s < sections.Length && !used; s++)
                    {
                        used = sections[s].MaterialIndex == i;
                    }

                    var marked = !material.IsVisible || !used;
                    var color = !material.IsVisible ? ImGui.GetColorU32(Settings.RedColor) : ImGui.GetColorU32(used ? ImGuiCol.Text : ImGuiCol.TextDisabled);
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    if (marked) ImGui.PushStyleColor(ImGuiCol.TabSelectedOverline, color);
                    var picked = ImGui.BeginTabItem($"{i}###Slot{i}");
                    ImGui.PopStyleColor(marked ? 2 : 1);

                    if (ImGui.IsItemHovered()) EditorUI.Tooltip($"{material.Name}{(used ? "" : "\nNot used in this LOD")}");
                    if (!picked) continue;

                    if (_materialIndex != i)
                    {
                        _materialIndex = i;
                        _materialLayerIndex = 0;
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.Unindent();

            EditorUI.PropertyValueTable(MaterialsLabel, () =>
            {
                var selected = SelectedMaterial;
                var container = selected?.MaterialDataContainer as MaterialDataContainer;

                EditorUI.PropertyWithToggle("Material", MaterialButtons);
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(selected?.Name ?? string.Empty);
                if (selected == null) ImGui.TextDisabled("Loading...");
                else if (container == null) ImGui.TextColored(Settings.OrangeColor, "No material data container available.");
                else if (!container.IsGpuDataReady) ImGui.TextColored(Settings.OrangeColor, "Uploading...");
                else if (selected.IsEdited) EditorUI.Caption("Edited");

                var indices = 0u;
                foreach (var section in sections)
                {
                    if (section.MaterialIndex == _materialIndex) indices += section.IndexCount;
                }
                EditorUI.Text("Indices", $"{indices} ({indices.GetReadableRatio(Descriptor.Lods[DisplayedLod].IndexCount)} of LOD {DisplayedLod})");

                if (container == null) return;

                EditorUI.Text("Blend", container.BlendMode.GetDescription());
                EditorUI.Text("Shading", container.ShadingModel.GetDescription());

                var layer = Math.Clamp(_materialLayerIndex, 0, container.LayerCount - 1);
                if (container.LayerCount > 1)
                {
                    EditorUI.PropertyWithToggle("Layer", LayerButtons);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted($"{layer + 1} / {container.LayerCount}");
                }

                EditorUI.Property("Textures");
                var spacing = style.ItemSpacing.X;
                var size = MathF.Min(3f, (ImGui.GetContentRegionAvail().X - spacing * 2f) / 3f / frame);
                EditorUI.DrawThumbnail(container.GetSlotTexture(layer, MaterialTextureSlot.Diffuse), "D", size);
                ImGui.SameLine(0, spacing);
                EditorUI.DrawThumbnail(container.GetSlotTexture(layer, MaterialTextureSlot.Normal), "N", size);
                ImGui.SameLine(0, spacing);
                EditorUI.DrawThumbnail(container.GetSlotTexture(layer, MaterialTextureSlot.Specular), "S", size);

                var color = container.Layers[layer].DiffuseColor;
                EditorUI.Property("Color");
                ImGui.ColorButton("##DiffuseColor", new Vector4(color, 1f), ImGuiColorEditFlags.NoPicker);
                ImGui.SameLine();
                ImGui.TextDisabled($"{color.X:0.00}, {color.Y:0.00}, {color.Z:0.00}");
            });
        }
    }

    private void DrawInfoPopup()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(viewport.WorkSize * 0.75f, ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f));

        var open = true;
        if (ImGui.BeginPopupModal("##PrimitiveInfo", ref open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
        {
            if (ImGui.BeginChild("##PrimitiveInfoBody", Vector2.Zero, ImGuiChildFlags.FrameStyle))
            {
                Descriptor.DrawControls();

                if (Metadata is { } metadata)
                {
                    ImGui.Spacing();
                    ImGui.SeparatorText("GPU Resources");
                    metadata.DrawControls();
                }
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }
}

/// <summary>
/// primitive component that uses a single section for the entire primitive data.
/// </summary>
public class PrimitiveComponent<TVertex, TPerMaterialData> : PrimitiveComponent<TVertex, PerInstanceData, TPerMaterialData>
    where TVertex : unmanaged
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected PrimitiveComponent(PrimitiveComponent<TVertex, TPerMaterialData> other) : base(other)
    {
        Materials = other.Materials;
    }

    protected PrimitiveComponent(TPrimitiveData<TVertex> primitive, CullingBounds bounds, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Descriptor = new PrimitiveDescriptor<TVertex>(bounds, () => primitive);
    }

    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        if (!IsVisible) SetMaterialsVisible(false);
    }

    protected PrimitiveComponent(USceneComponent component) : base(component)
    {
        if (!IsVisible) SetMaterialsVisible(false);
    }

    public sealed override MaterialSection[] Materials { get; } = [new(0)];

    public override object Clone() => new PrimitiveComponent<TVertex, TPerMaterialData>(this);
}

/// <inheritdoc />
public class PrimitiveComponent<TPerMaterialData> : PrimitiveComponent<Vector3, TPerMaterialData>
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected PrimitiveComponent(PrimitiveComponent<TPerMaterialData> other) : base(other)
    {

    }

    protected PrimitiveComponent(PrimitiveData primitive, CullingBounds bounds, Transform? transform = null, string? name = null) : base(primitive, bounds, transform, name)
    {

    }

    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {

    }
}

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent : PrimitiveComponent<PerMaterialData>
{
    protected PrimitiveComponent(PrimitiveComponent other) : base(other)
    {

    }

    public PrimitiveComponent(PrimitiveData primitive, Transform? transform = null, string? name = null) : base(primitive, new FBox(), transform, name)
    {

    }

    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {

    }
}
