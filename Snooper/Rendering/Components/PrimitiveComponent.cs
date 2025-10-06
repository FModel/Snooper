using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Primitive;
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
    private readonly LevelOfDetail<TVertex>[] _lods = [];
    public LevelOfDetail<TVertex>[] LevelOfDetails
    {
        get => _lods;
        protected init
        {
            if (value.Length == 0)
                throw new ArgumentException("There must be at least one LOD", nameof(value));
            
            _lods = value;
            
            Materials = new MaterialSection[_lods[0].SectionDescriptors.Length];
            for (var i = 0; i < Materials.Length; i++)
            {
                Materials[i] = new MaterialSection(_lods[0].SectionDescriptors[i].MaterialIndex);
            }
        }
    }
    public CullingBounds Bounds { get; protected init; }
    public string Path { get; protected init; }
    public readonly MaterialSection[] Materials; // we store materials for each section at lod 0

    public bool IsTranslucent => Materials.Any(m => m.IsTranslucent); // TODO: this is delayed by tasks

    public readonly bool IsVisible = true;
    
    protected PrimitiveComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {
        
    }

    protected PrimitiveComponent(LevelOfDetail<TVertex>[] levelOfDetails, CullingBounds bounds, Transform? transform = null, string? name = null) : base(transform, name)
    {
        LevelOfDetails = levelOfDetails;
        Bounds = bounds;
    }

    protected PrimitiveComponent(UPrimitiveComponent component) : base(component)
    {
        IsVisible = component.GetOrDefault("bVisible", IsVisible);
    }
    
    public void Generate(IndirectResources<TVertex, TInstanceData, TPerDrawData> resources, TextureManager textureManager)
    {
        resources.Add(Id, LevelOfDetails, Materials, GetPerInstanceData(), Bounds);
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
        
        EditorUI.CollapsingTable(Header, ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Path", Path);
            EditorUI.Text("Visible", IsVisible.ToString());
            EditorUI.Text("Draw ID", Materials[0].DrawMetadata.DrawId.ToString());
            EditorUI.Text("LODs", LevelOfDetails.Length.ToString());
            EditorUI.Text("Sections", Materials.Length.ToString());
        });
        
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
    protected PrimitiveComponent(TPrimitiveData<TVertex> primitive, CullingBounds bounds, Transform? transform = null, string? name = null) : base([new LevelOfDetail<TVertex>(FGuid.Random(), primitive)], bounds, transform, name)
    {
        
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
