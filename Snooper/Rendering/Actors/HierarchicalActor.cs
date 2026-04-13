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

    public override void DrawControls()
    {
        base.DrawControls();

        var cells = Children.OfType<CellActor>().ToArray();
        var visible = cells.Where(x => x.IsVisible).ToArray();
        var loadable = visible.Where(x => x is { CanLoad: true }).ToArray();

        var availWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonSize = new Vector2((availWidth - spacing) / 2, 0);

        ImGui.BeginDisabled(loadable.Length == 0);
        if (ImGui.Button($"Load {loadable.Length} Visible Cells", buttonSize))
        {
            ActorManager?.ThreadManager.EnqueueBatch(loadable.Select(cell => cell.GetLoadAction()).OfType<Action>().ToList());
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(cells.Length > 50);
        if (ImGui.Button($"Load All {cells.Length} Cells", buttonSize))
        {
            ActorManager?.ThreadManager.EnqueueBatch(cells.Select(cell => cell.GetLoadAction()).OfType<Action>().ToList());
        }
        ImGui.EndDisabled();
    }
}
