using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;
using System.Numerics;
using ImGuiNET;

namespace Snooper.Rendering.Actors;

public class HierarchicalActor : Actor
{
    public Vector2 Range { get; }

    public HierarchicalActor(FRuntimePartitionStreamingData hlod, float minRange = 0f) : base(hlod.Name.ToString())
    {
        Components.Add(new SpatialComponent(null, "HLODRoot"));

        Range = new Vector2(minRange, hlod.LoadingRange * Settings.GlobalScale);

        var color = new Vector3(
            (MathF.Sin(Range.Y * 0.1f + 0) + 1) * 0.5f,
            (MathF.Sin(Range.Y * 0.1f + 2) + 1) * 0.5f,
            (MathF.Sin(Range.Y * 0.1f + 4) + 1) * 0.5f
        );

        ProcessStreamingCells(hlod.SpatiallyLoadedCells, color);
        ProcessStreamingCells(hlod.NonSpatiallyLoadedCells, color, true);
    }

    public HierarchicalActor(FSpatialHashStreamingGrid grid, float minRange = 0f) : base(grid.GridName.ToString())
    {
        var origin = new Vector3(grid.Origin.X, grid.Origin.Z, grid.Origin.Y) * Settings.GlobalScale;
        Components.Add(new SpatialComponent(new Transform(origin), "GridRoot"));

        Range = new Vector2(minRange, grid.LoadingRange * Settings.GlobalScale);

        var color = new Vector3(grid.DebugColor.R, grid.DebugColor.G, grid.DebugColor.B);
        foreach (var level in grid.GridLevels)
        {
            foreach (var cell in level.LayerCells)
            {
                ProcessStreamingCells(cell.GridCells, color);
            }
        }
    }

    private void ProcessStreamingCells(FPackageIndex[] ptrs, Vector3? color = null, bool isPersistent = false)
    {
        foreach (var ptr in ptrs)
        {
            if (!ptr.TryLoad<UWorldPartitionRuntimeCell>(out var cell))
                continue;

            Children.Add(new CellActor(cell, color, isPersistent));
        }
    }

    public void LoadAround(Vector3 position)
    {
        var cells = Children.OfType<StreamableActor>()
            .Where(x => x is { IsPersistent: false, IsVisible: true })
            .Select(x => (Cell: x, Distance: x.DistanceTo(position)))
            .OrderBy(x => x.Distance);

        foreach (var (cell, distance) in cells)
        {
            if (distance >= Range.X && distance <= Range.Y) cell.Load();
            else cell.Unload();
        }
    }

    public void UnloadAll()
    {
        foreach (var cell in Children.OfType<StreamableActor>().Where(x => !x.IsPersistent))
        {
            cell.Unload();
        }
    }

    public override void DrawControls()
    {
        base.DrawControls();

        var total = 0;
        var loaded = 0;
        var loading = 0;
        foreach (var child in Children)
        {
            if (child is not StreamableActor cell) continue;

            total++;
            if (cell.IsLoaded) loaded++;
            else if (cell.IsLoading) loading++;
        }

        EditorUI.PropertyValueTable("Cells", () =>
        {
            EditorUI.Text("Range", $"{Range.X:F0} to {Range.Y:F0}");
            EditorUI.Text("Loaded", $"{loaded:N0} / {total:N0}");
            EditorUI.Text("Loading", $"{loading:N0}");
        });

        DrawLoadControls(this, LoadAround, UnloadAll);
    }

    internal static void DrawLoadControls(Actor owner, Action<Vector3> loadAround, Action unloadAll)
    {
        var camera = owner.ActorManager is SceneManager { MainViewport.Camera: { } main } ? main : null;

        var availWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonSize = new Vector2((availWidth - spacing) / 2, 0);

        ImGui.BeginDisabled(camera is null);
        if (ImGui.Button("Load Around Camera", buttonSize) && camera is not null)
        {
            loadAround(camera.GetLocalTransform().Position);
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Unload All", buttonSize))
        {
            unloadAll();
        }
    }
}
