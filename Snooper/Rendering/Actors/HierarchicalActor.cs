using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;

namespace Snooper.Rendering.Actors;

public class HierarchicalActor : Actor
{
    public float LoadingRange { get; }

    public HierarchicalActor(FRuntimePartitionStreamingData hlod, int index, bool load) : base(hlod.Name.ToString())
    {
        Components.Add(new SpatialComponent(null, "HLODRoot"));

        LoadingRange = hlod.LoadingRange * Settings.GlobalScale;

        var hue = index * 0.618033988749895f % 1f;
        var h = hue * 6;
        var x = 1 - MathF.Abs(h % 2 - 1);
        var color = h switch
        {
            < 1 => new Vector3(1, x, 0),
            < 2 => new Vector3(x, 1, 0),
            < 3 => new Vector3(0, 1, x),
            < 4 => new Vector3(0, x, 1),
            < 5 => new Vector3(x, 0, 1),
            _ => new Vector3(1, 0, x)
        } * 0.5f;

        ProcessStreamingCells(hlod.SpatiallyLoadedCells, color, load);
        ProcessStreamingCells(hlod.NonSpatiallyLoadedCells, color, load);
    }

    public HierarchicalActor(FSpatialHashStreamingGrid grid) : base(grid.GridName.ToString())
    {
        var origin = new Vector3(grid.Origin.X, grid.Origin.Z, grid.Origin.Y) * Settings.GlobalScale;
        Components.Add(new SpatialComponent(new Transform(origin), "GridRoot"));

        LoadingRange = grid.LoadingRange * Settings.GlobalScale;

        var color = new Vector3(grid.DebugColor.R, grid.DebugColor.G, grid.DebugColor.B);
        foreach (var level in grid.GridLevels)
        {
            foreach (var cell in level.LayerCells)
            {
                ProcessStreamingCells(cell.GridCells, color);
            }
        }
    }

    private void ProcessStreamingCells(FPackageIndex[] ptrs, Vector3? color = null, bool load = false)
    {
        foreach (var ptr in ptrs)
        {
            if (!ptr.TryLoad<UWorldPartitionRuntimeCell>(out var cell))
                continue;

            Children.Add(new CellActor(cell, color, load));
        }
    }

    public void UpdateCellVisibility(Vector3 position, float minDistance = 0f)
    {
        if (!IsVisible) return;

        foreach (var cell in Children.OfType<CellActor>())
        {
            var distance = Vector3.Distance(position, cell.Center);
            cell.IsVisible = distance > minDistance && distance <= LoadingRange;
            // if (cell.IsVisible && cell is { IsLoaded: false, IsLoading: false })
            // {
            //     cell.Load();
            // }
        }
    }
}
