using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class HierarchicalActor : Actor
{
    public float Index { get; }
    public float LoadingRange { get; }
    
    public HierarchicalActor(FRuntimePartitionStreamingData hlod, int index, bool load) : base(hlod.Name.ToString())
    {
        IsVisible = false;
        Components.Add(new SpatialComponent(null, "HLODRoot"));

        Index = index;
        LoadingRange = hlod.LoadingRange * Settings.GlobalScale;
        
        ProcessStreamingCells(hlod.SpatiallyLoadedCells, load);
        ProcessStreamingCells(hlod.NonSpatiallyLoadedCells, load);
    }
    
    private void ProcessStreamingCells(FPackageIndex[] ptrs, bool load)
    {
        foreach (var ptr in ptrs)
        {
            if (!ptr.TryLoad<UWorldPartitionRuntimeCell>(out var cell))
                continue;

            Children.Add(new CellActor(cell, load));
        }
    }
}