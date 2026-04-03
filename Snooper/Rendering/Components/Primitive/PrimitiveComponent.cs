using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Actors;
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

    public ResourcesMetadata? Metadata { get; private set; }

    public abstract MaterialSection[] Materials { get; }

    private bool? _isOpaque;
    public bool IsOpaque
    {
        get => _isOpaque ??= SupportsOpaquePass;
        private set
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

    /// <summary>
    /// opaque pass requires shader support for writing to multiple render targets, so by default it's disabled and primitives are rendered in the translucent pass
    /// </summary>
    protected virtual bool SupportsOpaquePass => false;

    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        if (component.TryGetValue(out bool visible, "bVisible"))
        {
            IsVisible = visible;
        }
        else if (component.TryGetValue(out bool hidden, "bHiddenInGame"))
        {
            IsVisible = !hidden;
        }

        if (component.TryGetValue(out bool castShadow, "CastShadow", "bCastStaticShadow", "bCastDynamicShadow"))
        {
            CastShadow = castShadow;
        }
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerMaterialData> resources)
    {
        if (Metadata is null)
        {
            Metadata = resources.Add(this);

            // in our current setup, we first upload the geometry data to the GPU and THEN we set up the material sections
            // but it's very much possible here that the material section already has its material data container set
            // in which case, by subscribing to OnMaterialDataContainerSet it will be triggered immediately
            foreach (var material in Materials)
            {
                // basically the component is responsible for setting a material container for each section
                // once the container is set, OnMaterialDataContainerSet will be triggered and all the textures in the container will be sent to the texture cache
                // the cache will generate all these textures, make them bindless, finalize the container's GPU data, and then trigger OnContainerReady
                // OnContainerReady will upload the material data to the GPU for this component, in the system that owns it
                // material containers and textures are CPU cached globally, but duplicated on the GPU, per section, per component, per system...

                material.OnContainerReady += resources.Update;
                material.OnMaterialDataContainerSet += section =>
                {
                    TextureCache.Add(section);
                    IsOpaque &= !section.IsTranslucent;
                };
            }
        }
        else
        {
            resources.Update(this);
        }
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

    protected override (Vector3, float) GetTeleportPosition(CameraComponent camera)
    {
        var matrices = GetWorldMatrices();
        if (matrices.Length == 0) return (Vector3.Zero, 1.0f);

        var overallCenter = Vector3.Zero;
        foreach (var matrix in matrices)
        {
            var worldCenter = Vector3.Transform(Descriptor.Bounds.Center, matrix);
            overallCenter += worldCenter;
        }
        overallCenter /= matrices.Length;

        var extents = Descriptor.Bounds.Extents;
        var maxExtent = MathF.Max(extents.X, MathF.Max(extents.Y, extents.Z));
        var distance = maxExtent * 1.25f / MathF.Tan(camera.FieldOfViewRadians / 2f);

        return (overallCenter, MathF.Max(distance, 0.1f));
    }

    protected override void OnActorAttached(Actor actor)
    {
        base.OnActorAttached(actor);

        if (!actor.IsVisible) IsVisible = false;
    }

    public override string Icon => "\ue4e2";

    private HeaderButtons HeaderButtons => field ??= new HeaderButtons()
        .Add("\uf0c5", "Copy Path", () => ImGui.SetClipboardText(Descriptor.Path))
        .Add(() => IsVisible ? Settings.EyeIcon : Settings.EyeSlashIcon, () => "Toggle Visibility",
            () => { IsVisible = !IsVisible; }, null,
            () => IsVisible ? null : Settings.RedColor);

    private int _sectionIndex;
    private int _materialIndex;
    public override void DrawControls()
    {
        base.DrawControls();

        var open = ImGui.CollapsingHeader("Mesh", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (!open) return;

        EditorUI.PropertyValueTable("Descriptor", () =>
        {
            EditorUI.Text("Guid", Descriptor.Guid.ToString(EGuidFormats.UniqueObjectGuid));

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

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
            ImGui.SetWindowFontScale(0.85f);

            var lod = Descriptor.Lods[Math.Max(0, value)];
            switch (value)
            {
                case -1:
                    ImGui.TextUnformatted("Auto (Screen Size Based)");
                    break;
                case >= 0 when value < Descriptor.Lods.Length:
                    ImGui.TextUnformatted($"{lod.VertexCount} Vertices, {lod.IndexCount} Indices");
                    break;
            }

            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopStyleVar();
            ImGui.Spacing();
            ImGui.EndGroup();

            EditorUI.Property($"Sections ({lod.Sections.Length})");
            ImGui.BeginGroup();
            if (lod.Sections.Length > 0)
            {
                var maxSection = lod.Sections.Length - 1;

                ImGui.BeginDisabled(maxSection == 0);
                var slided2 = ImGui.SliderInt("##SectionSlider", ref _sectionIndex, 0, maxSection);
                ImGui.EndDisabled();

                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                ImGui.SetWindowFontScale(0.85f);

                var section = lod.Sections[_sectionIndex];
                if (slided1 || slided2) _materialIndex = (int)section.MaterialIndex;
                ImGui.TextUnformatted($"{section.Name}: Material {section.MaterialIndex}, Shadows? {section.CastShadow && CastShadow}");

                ImGui.SetWindowFontScale(1.0f);
                ImGui.PopStyleVar();
                ImGui.Spacing();
            }
            else
            {
                ImGui.TextDisabled("No Sections?");
            }
            ImGui.EndGroup();
        });

        EditorUI.PropertyValueTable("Materials", () =>
        {
            EditorUI.Property($"Materials ({Materials.Length})");
            ImGui.BeginGroup();

            MaterialSection? material = null;
            if (Materials.Length > 0)
            {
                var maxMaterial = Materials.Length - 1;

                if (maxMaterial == 0) ImGui.BeginDisabled();
                ImGui.SliderInt("##MaterialSlider", ref _materialIndex, 0, maxMaterial);
                if (maxMaterial == 0) ImGui.EndDisabled();

                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                ImGui.SetWindowFontScale(0.85f);

                material = Materials[_materialIndex];
                ImGui.TextUnformatted($"{material.MaterialDataContainer?.Name ?? Settings.NoName} (offset {material.Allocation?.StartIndex ?? -1})");

                ImGui.SetWindowFontScale(1.0f);
                ImGui.PopStyleVar();
                ImGui.Spacing();
            }

            ImGui.EndGroup();

            if (material?.MaterialDataContainer != null)
            {
                material.MaterialDataContainer.DrawControls();
            }
            else
            {
                EditorUI.Property("Data Container");
                ImGui.TextColored(Settings.OrangeColor, "No material data container assigned");
            }
        });

        if (ImGui.TreeNodeEx("Metadata", ImGuiTreeNodeFlags.None))
        {
            if (Metadata is { } metadata)
            {
                metadata.DrawControls();
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "No resources allocated");
            }
            ImGui.TreePop();
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

    public sealed override MaterialSection[] Materials { get; } = [new(0)];
}

/// <inheritdoc />
public class PrimitiveComponent<TPerMaterialData> : PrimitiveComponent<Vector3, TPerMaterialData>
    where TPerMaterialData : unmanaged, IPerMaterialData
{
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
