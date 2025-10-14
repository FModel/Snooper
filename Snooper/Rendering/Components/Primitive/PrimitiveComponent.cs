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
    
    public ResourcesMetadata Metadata { get; private set; }
    
    public abstract MaterialSection[] Materials { get; }

    public bool IsTranslucent => Materials.Any(m => m.IsTranslucent); // TODO: this is delayed by tasks

    public bool IsVisible { get; protected init; } = true;
    public int OverrideLod { get; protected set; } = -1;
    
    
    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {
    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        IsVisible = component.GetOrDefault("bVisible", IsVisible);
    }

    public void Generate(IndirectResources<TVertex, TInstanceData, TPerMaterialData> resources, TextureManager textureManager)
    {
        Metadata = resources.Add(Id, Descriptor, Materials, GetPerInstanceData());
        textureManager.AddRange(Materials);
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerMaterialData> resources, TextureManager textureManager)
    {
        if (!Metadata.IsGenerated)
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
                    var value = OverrideLod;
                    var maxLod = Descriptor.Lods.Length - 1;
                    
                    if (!IsVisible || maxLod == 0) ImGui.BeginDisabled();
                    if (ImGui.SliderInt("##LODSlider", ref value, minLod, maxLod)) OverrideLod = value;
                    if (!IsVisible || maxLod == 0) ImGui.EndDisabled();
                    
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                    ImGui.SetWindowFontScale(0.85f);
                    
                    var lod = Descriptor.Lods[Math.Max(0, OverrideLod)];
                    switch (OverrideLod)
                    {
                        case -1:
                            ImGui.TextUnformatted("Auto (Screen Size Based)");
                            break;
                        case >= 0 when OverrideLod < Descriptor.Lods.Length:
                            ImGui.TextUnformatted($"LOD {OverrideLod}: {lod.VertexCount} vertices, {lod.IndexCount} indices, {lod.ScreenSize} screen size");
                            break;
                    }
                    
                    ImGui.SetWindowFontScale(1.0f);
                    ImGui.PopStyleVar();
                    ImGui.EndGroup();
                    
                    EditorUI.Property($"Sections ({lod.Sections.Length})");
                    ImGui.BeginGroup();
                    
                    if (lod.Sections.Length > 0)
                    {
                        var sectionIndex = 0;
                        var maxSection = lod.Sections.Length - 1;
                        
                        if (maxSection == 0) ImGui.BeginDisabled();
                        ImGui.SliderInt("##SectionSlider", ref sectionIndex, 0, maxSection);
                        if (maxSection == 0) ImGui.EndDisabled();
                        
                        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                        ImGui.SetWindowFontScale(0.85f);
                        
                        var section = lod.Sections[sectionIndex];
                        ImGui.TextUnformatted($"Section {sectionIndex}: Material {section.MaterialIndex}, {section.IndexCount} indices (offset {section.FirstIndex})");
                        
                        ImGui.SetWindowFontScale(1.0f);
                        ImGui.PopStyleVar();
                    }
                    else
                    {
                        ImGui.TextDisabled("No Sections?");
                    }
                    
                    ImGui.EndGroup();
                });
            });

            EditorUI.SharedTreeNode("Materials", ImGuiTreeNodeFlags.None, Id, () =>
            {
                EditorUI.PropertyValueTable("Materials", () =>
                {
                    EditorUI.Property($"Materials ({Materials.Length})");
                    ImGui.BeginGroup();
            
                    if (Materials.Length > 0)
                    {
                        var materialIndex = 0;
                        var maxMaterial = Materials.Length - 1;
                        
                        if (maxMaterial == 0) ImGui.BeginDisabled();
                        ImGui.SliderInt("##MaterialSlider", ref materialIndex, 0, maxMaterial);
                        if (maxMaterial == 0) ImGui.EndDisabled();
                        
                        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
                        ImGui.SetWindowFontScale(0.85f);
                        
                        var material = Materials[materialIndex];
                        ImGui.TextUnformatted($"Material {materialIndex}: Material {material.MaterialIndex}, Offset {material.MaterialOffset}");
                        
                        ImGui.SetWindowFontScale(1.0f);
                        ImGui.PopStyleVar();
                    }
                    else
                    {
                        ImGui.TextDisabled("No Materials?");
                    }
            
                    ImGui.EndGroup();
            
                    // foreach (var material in Materials)
                    // {
                    //     material.MaterialDataContainer?.DrawControls();
                    // }
                });
            });
            
            EditorUI.SharedTreeNode("Metadata", ImGuiTreeNodeFlags.None, Id, () =>
            {
                if (ImGui.BeginTable("Metadata", 4))
                {
                    ImGui.TableSetupColumn("Property1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Value1", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                    ImGui.TableSetupColumn("Property2", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Value2", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                    ImGui.Indent();
                    
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("ID");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(Id.ToString());
                    
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("Geometry");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(Metadata.BaseGeometry.ToString());
                    ImGui.TableSetColumnIndex(2);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("Instances");
                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(Metadata.BaseInstance.ToString());
                    
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("Materials");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(Metadata.BaseMaterial.ToString());
                    ImGui.TableSetColumnIndex(2);
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted($"Draw IDs ({Metadata.DrawIds.Length})");
                    ImGui.TableSetColumnIndex(3);
                    ImGui.TextUnformatted(string.Join(", ", Metadata.DrawIds));
                    
                    ImGui.Unindent();
                    ImGui.EndTable();
                }
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

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        
    }
}