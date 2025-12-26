using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;
using ImGuiNET;

namespace Snooper.Rendering.Actors;

public class HierarchicalActor : Actor
{
    public float LoadingRange { get; }

    public HierarchicalActor(FRuntimePartitionStreamingData hlod) : base(hlod.Name.ToString())
    {
        Components.Add(new SpatialComponent(null, "HLODRoot"));

        LoadingRange = hlod.LoadingRange * Settings.GlobalScale;

        var color = new Vector3(
            (MathF.Sin(LoadingRange * 0.1f + 0) + 1) * 0.5f,
            (MathF.Sin(LoadingRange * 0.1f + 2) + 1) * 0.5f,
            (MathF.Sin(LoadingRange * 0.1f + 4) + 1) * 0.5f
        );

        ProcessStreamingCells(hlod.SpatiallyLoadedCells, color);
        ProcessStreamingCells(hlod.NonSpatiallyLoadedCells, color, true);
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

    private void ProcessStreamingCells(FPackageIndex[] ptrs, Vector3? color = null, bool isNonSpatiallyLoaded = false)
    {
        foreach (var ptr in ptrs)
        {
            if (!ptr.TryLoad<UWorldPartitionRuntimeCell>(out var cell))
                continue;

            Children.Add(new CellActor(cell, color, isNonSpatiallyLoaded));
        }
    }

    public void SetVisibilityByDistance(Vector3 position, float minDistance = 0f)
    {
        foreach (var cell in Children.OfType<CellActor>().Where(x => x is { IsNonSpatiallyLoaded: false, DataLayers.Length: 0 }))
        {
            var distance = Vector3.Distance(position, cell.RootComponent?.LocalTransform.Position ?? Vector3.Zero);
            cell.IsVisible = distance > minDistance && distance <= LoadingRange;
        }
    }

    internal override void DrawInterface()
    {
        base.DrawInterface();

        ImGui.SeparatorText("HLOD Cells");

        var visible = Children.Where(x => x.IsVisible).ToArray();
        var cells = visible.OfType<CellActor>().Where(x => x is { CanLoad: true, IsLoaded: false, IsLoading: false }).ToArray();

        ImGui.Text($"Visible Cells: {visible.Length}/{Children.Count} (Loadable: {cells.Length})");

        ImGui.BeginDisabled(cells.Length == 0);
        var width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImGui.Button($"Load {cells.Length} Cells", width))
        {
            foreach (var cell in cells) cell.Load();
        }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(Children.Count > 50);
        if (ImGui.Button("Load All Cells", width))
        {
            foreach (var cell in Children.OfType<CellActor>()) cell.Load();
        }
        ImGui.EndDisabled();
    }
}
