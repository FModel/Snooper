using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.FastGeoStreaming;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Core.Containers.Resources;
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
    public ResourcesMetadata? Metadata { get; }
    public MaterialSection[] Materials { get; }
    public bool IsOpaque { get; }
    public bool IsVisible { get; set; }

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

    public bool IsVisible
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            MarkDirty(DirtyFlags.Visibility);
        }
    } = true;

    public readonly bool CastShadow = true;
    public readonly Vector2 DrawDistance = Vector2.Zero;

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
        if (component.TryGetValue(out bool visible, "bVisible"))
        {
            IsVisible = visible;
        }
        else if (component.TryGetValue(out bool hidden, "bHiddenInGame", "bIsHidden"))
        {
            IsVisible = !hidden;
        }

        if (component.TryGetValue(out bool castShadow, "CastShadow", "bCastStaticShadow", "bCastDynamicShadow"))
        {
            CastShadow = castShadow;
        }

        if (component.TryGetValue(out float minDrawDistance, "MinDrawDistance"))
        {
            DrawDistance.X = minDrawDistance * Settings.GlobalScale;
        }
        if (component.TryGetValue(out float maxDrawDistance, "CachedMaxDrawDistance"))
        {
            DrawDistance.Y = maxDrawDistance * Settings.GlobalScale;
        }
    }

    protected PrimitiveComponent(FFastGeoPrimitiveComponent component) : base(component)
    {
        var desc = component.SceneProxyDesc.PrimitiveSceneProxyDesc;
        IsVisible = component.bVisible || !desc.bIsHidden;
        CastShadow = desc.CastShadow || desc.bCastStaticShadow || desc.bCastDynamicShadow;
        DrawDistance.X = desc.MinDrawDistance * Settings.GlobalScale;
        DrawDistance.Y = desc.CachedMaxDrawDistance * Settings.GlobalScale;
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

    public void SnapToGround()
    {
        var lowest = float.MaxValue;
        for (var i = 0; i < 8; i++)
        {
            lowest = MathF.Min(lowest, Vector3.Transform(GetBoundsCorner(i), WorldMatrix).Y);
        }

        if (lowest == 0f || !Matrix4x4.Invert(GetRelationMatrix(), out var invRelation))
            return;

        var transform = GetLocalTransform();
        transform.Position -= Vector3.TransformNormal(new Vector3(0f, lowest, 0f), invRelation);
        SetLocalTransform(transform);
    }

    protected override void BeginPlay(ActorManager scene)
    {
        base.BeginPlay(scene);

        if (Actor is { IsVisible: false }) IsVisible = false;
    }

    public override string Icon => "\ue4e2";

    private const string HeaderLabel = "Mesh";
    private HeaderButtons HeaderButtons => field ??= new HeaderButtons(HeaderLabel)
        .Add(() => IsVisible ? Settings.EyeIcon : Settings.EyeSlashIcon, () => "Toggle Visibility",
            () => { IsVisible = !IsVisible; }, null,
            () => IsVisible ? null : Settings.RedColor)
        .Add("\uf0c5", "Copy Path", () => ImGui.SetClipboardText(Descriptor.Path))
        .Add("\uf05a", "Primitive Info", () => ImGui.OpenPopup("##PrimitiveInfo"));

    private PropertyToggleButton[] MaterialButtons => field ??=
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
            visible: () => MaterialLayerCount > 1),
        new PropertyToggleButton(
            () => Settings.PaletteIcon,
            () => WindowRequests.Request(Settings.MaterialEditorWindow),
            () => "Material Editor",
            () => SelectedMaterial?.MaterialDataContainer != null)
    ];

    private PropertyToggleButton[] MorphButtons => field ??=
    [
        new PropertyToggleButton(
            () => Settings.BarsProgressIcon,
            () => WindowRequests.Request(Settings.MorphEditorWindow),
            () => "Morph Editor",
            () => Descriptor.Morphs is { Count: > 0 })
    ];

    private int _sectionIndex;
    private int _materialIndex;
    private int _materialLayerIndex;
    private int MaterialLayerCount => SelectedMaterial?.MaterialDataContainer is MaterialDataContainer material ? material.LayerCount : 0;
    public MaterialSection? SelectedMaterial => _materialIndex >= 0 && _materialIndex < Materials.Length ? Materials[_materialIndex] : null;

    public override void DrawControls()
    {
        base.DrawControls();
        if (string.IsNullOrEmpty(Descriptor.Name))
            return;

        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        DrawInfoPopup();

        if (!open) return;

        EditorUI.PropertyValueTable(HeaderLabel, () =>
        {
            EditorUI.Text("Name", Descriptor.Name);

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
                _sectionIndex = 0;
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
                    EditorUI.Caption($"{lod.VertexCount} Vertices, {lod.IndexCount} Indices");
                    break;
            }

            ImGui.Spacing();
            ImGui.EndGroup();

            if (DrawDistance != Vector2.Zero)
            {
                EditorUI.Text("Draw Distance", $"Min: {DrawDistance.X}, Max: {DrawDistance.Y}");
            }

            EditorUI.Property($"Sections ({lod.Sections.Length})");
            var selected = lod.Sections[_sectionIndex];
            _materialIndex = (int) selected.MaterialIndex;

            ImGui.BeginGroup();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##SectionCombo", $"{_sectionIndex}: {selected.Name}"))
            {
                for (var i = 0; i < lod.Sections.Length; i++)
                {
                    var isSelected = i == _sectionIndex;

                    if (ImGui.Selectable($"{i}: {lod.Sections[i].Name}##Section{i}", isSelected))
                    {
                        _sectionIndex = i;
                        _materialLayerIndex = 0;
                        _materialIndex = (int) lod.Sections[i].MaterialIndex;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            EditorUI.Caption($"Material {selected.MaterialIndex}, {(selected.CastShadow && CastShadow ? "casts shadow" : "no shadow")}");
            ImGui.Spacing();
            ImGui.EndGroup();

            EditorUI.PropertyWithToggle("Material", MaterialButtons);
            if (SelectedMaterial is not { } section)
            {
                ImGui.TextDisabled("Loading...");
            }
            else if (section.MaterialDataContainer is not MaterialDataContainer container)
            {
                ImGui.TextColored(Settings.OrangeColor, "No material data container available.");
            }
            else
            {
                container.DrawSummary(_materialLayerIndex);
            }

            if (Descriptor.Morphs is { Count: > 0 } morphs)
            {
                EditorUI.PropertyWithToggle($"Morph Targets ({morphs.Count})", MorphButtons);
                ImGui.Text(string.Empty);
            }
        });
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

    }

    protected PrimitiveComponent(USceneComponent component) : base(component)
    {

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
