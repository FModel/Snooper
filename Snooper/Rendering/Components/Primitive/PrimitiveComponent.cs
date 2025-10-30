using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Primitive;

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> : SpatialComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    private readonly PrimitiveDescriptor<TVertex>? _descriptor;
    public PrimitiveDescriptor<TVertex> Descriptor
    {
        get => _descriptor ?? throw new InvalidOperationException($"Descriptor not initialized for {Name} of type {GetType().Name}.");
        protected init => _descriptor = value;
    }
    
    public ResourcesMetadata? Metadata { get; private set; }
    
    public abstract MaterialSection[] Materials { get; }

    public bool IsOpaque => !Materials.Any(m => m.IsTranslucent); // TODO: this is delayed by tasks

    public bool IsVisible { get; protected init; } = true;
    
    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {
        
    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        IsVisible = component.GetOrDefault("bVisible", IsVisible);
    }

    public void Generate(IndirectResources<TVertex, TInstanceData, TPerMaterialData> resources, TextureManager textureManager)
    {
        Metadata = resources.Add(this);
        textureManager.AddRange(Materials);
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerMaterialData> resources, TextureManager textureManager)
    {
        if (Metadata is null)
        {
            Generate(resources, textureManager);
        }
        else
        {
            resources.Update(this);
        }
    }
    
    private TInstanceData[]? _cachedInstanceData { get; set; }
    public TInstanceData[] GetPerInstanceData()
    {
        var matrices = GetInstanceMatrices();
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
    
    internal override string Icon => "primitive";

    private int _sectionIndex;
    private int _materialIndex;
    public override void DrawControls()
    {
        base.DrawControls();
        
        if (ImGui.CollapsingHeader(Header, ImGuiTreeNodeFlags.DefaultOpen))
        {
            EditorUI.SharedTreeNode("Descriptor", ImGuiTreeNodeFlags.DefaultOpen, Id, () =>
            {
                EditorUI.PropertyValueTable("Descriptor", () =>
                {
                    EditorUI.Text("Path", Descriptor.Path ?? "N/A");
                    EditorUI.Text("Guid", Descriptor.Guid.ToString(EGuidFormats.UniqueObjectGuid));
                    EditorUI.Text("Is Visible", IsVisible.ToString());
                    
                    EditorUI.Property($"LODs ({Descriptor.Lods.Length})");
                    ImGui.BeginGroup();

                    const int minLod = -1;
                    var maxLod = Descriptor.Lods.Length - 1;
                    var value = Metadata == null ? minLod : Metadata.Value.GeometryHandle.OverrideLod;

                    var slided1 = ImGui.SliderInt("##LODSlider", ref value, minLod, maxLod);
                    var lod = Descriptor.Lods[Math.Max(0, value)];
                    if (slided1)
                    {
                        _sectionIndex = 0;
                        if (Metadata != null && IsVisible && maxLod > 0)
                        {
                            Metadata.Value.GeometryHandle.OverrideLod = value;
                        }
                    }

                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                    ImGui.SetWindowFontScale(0.85f);
                    
                    switch (value)
                    {
                        case -1:
                            ImGui.TextUnformatted("Auto (Screen Size Based)");
                            break;
                        case >= 0 when value < Descriptor.Lods.Length:
                            ImGui.TextUnformatted($"{lod.VertexCount} Vertices, {lod.IndexCount} Indices, {lod.LayerCount} UV{(lod.LayerCount > 1 ? "s" : "")}, {lod.ScreenSize} Screen Size");
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
                        ImGui.TextUnformatted($"{section.Name}: Material {section.MaterialIndex}, {section.IndexCount} Indices (offset {section.FirstIndex})");
                        
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
            });

            EditorUI.SharedTreeNode("Materials", ImGuiTreeNodeFlags.DefaultOpen, Id, () =>
            {
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
                        ImGui.TextUnformatted($"{material.Name} (offset {material.Allocation?.StartIndex ?? -1})");
                        
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
                        ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "No material data container assigned");
                    }
                });
            });
            
            EditorUI.SharedTreeNode("Metadata", ImGuiTreeNodeFlags.None, Id, () =>
            {
                ImGui.Indent();
                if (Metadata is { } metadata)
                {
                    metadata.DrawControls();
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "No resources allocated");
                }
                ImGui.Unindent();
            });
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