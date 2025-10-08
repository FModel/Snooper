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

namespace Snooper.Rendering.Components;

public abstract class PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> : SpatialComponent
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    private readonly PrimitiveDescriptor2<TVertex>? _descriptor;
    public PrimitiveDescriptor2<TVertex> Descriptor
    {
        get => _descriptor ?? throw new InvalidOperationException("Descriptor has not been initialized. Set it during construction of derived classes.");
        protected init
        {
            _descriptor = value;
            
            // init materials for the first LOD only
            Materials = new MaterialSection[_descriptor.Lods[0].Sections.Length];
            for (var i = 0; i < Materials.Length; i++)
            {
                Materials[i] = new MaterialSection(_descriptor.Lods[0].Sections[i].MaterialIndex);
            }
        }
    }

    public MaterialSection[] Materials { get; private init; } = [];

    public bool IsTranslucent => Materials.Any(m => m.IsTranslucent); // TODO: this is delayed by tasks

    public readonly bool IsVisible = true;
    
    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {
    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        IsVisible = component.GetOrDefault("bVisible", IsVisible);
    }

    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        resources.Add(Id, Descriptor, Materials, GetPerInstanceData());
        textureManager.AddRange(Materials);
    }

    public void Update(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        if (!Materials[0].IsGenerated)
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

    public override void DrawControls()
    {
        base.DrawControls();
        
        if (ImGui.CollapsingHeader(Header, ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.TreeNodeEx("Descriptor", ImGuiTreeNodeFlags.DefaultOpen))
            {
                EditorUI.PropertyValueTable(Header, () =>
                {
                    EditorUI.Text("Path", Descriptor.Path ?? "N/A");
                    EditorUI.Text("Guid", Descriptor.Guid.ToString(EGuidFormats.UniqueObjectGuid));
                    
                    // TODO: more shit
                    
                    var i = 0;
                    EditorUI.Property("LODs");
                    if (ImGui.BeginCombo("##LODs", $"LOD {i} - {Descriptor.Lods[i].VertexCount} vertices, {Descriptor.Lods[i].IndexCount} indices"))
                    {
                        for (i = 0; i < Descriptor.Lods.Length; i++)
                        {
                            if (ImGui.Selectable($"LOD {i} - {Descriptor.Lods[i].VertexCount} vertices, {Descriptor.Lods[i].IndexCount} indices"))
                            {
                                // TODO: alter the culling to force the preview of the selected LOD
                            }
                        }

                        ImGui.EndCombo();
                    }
                });
                
                ImGui.TreePop();
            }
        }
        
        EditorUI.CollapsingTable("Materials", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            foreach (var material in Materials)
            {
                material.DrawDataContainer?.DrawControls();
            }
        });
    }
}

/// <summary>
/// primitive component that uses a single section for the entire primitive data.
/// </summary>
public class PrimitiveComponent<TVertex, TPerDrawData> : PrimitiveComponent<TVertex, PerInstanceData, TPerDrawData>
    where TVertex : unmanaged
    where TPerDrawData : unmanaged, IPerDrawData
{
    protected PrimitiveComponent(TPrimitiveData<TVertex> primitive, CullingBounds bounds, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Descriptor = new PrimitiveDescriptor2<TVertex>(bounds, () => primitive);
    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
    }
}

/// <inheritdoc />
public class PrimitiveComponent<TPerDrawData>(PrimitiveData primitive, CullingBounds bounds, Transform? transform = null, string? name = null)
    : PrimitiveComponent<Vector3, TPerDrawData>(primitive, bounds, transform, name)
    where TPerDrawData : unmanaged, IPerDrawData;

/// <inheritdoc />
[DefaultActorSystem(typeof(PrimitiveSystem))]
public class PrimitiveComponent(PrimitiveData primitive, Transform? transform = null, string? name = null) : PrimitiveComponent<PerDrawData>(primitive, new FBox(), transform, name);
