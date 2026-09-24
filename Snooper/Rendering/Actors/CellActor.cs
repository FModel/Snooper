using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;
using Snooper.UI;

namespace Snooper.Rendering.Actors;

public class CellActor : StreamableActor
{
    public string[] DataLayers { get; }
    public bool IsHLOD { get; }

    private readonly FSoftObjectPath? _world;
    private readonly Vector3? _loadingExtents;

    public CellActor(UWorldPartitionRuntimeCell cell, Vector3? color = null, bool isPersistent = false) : base(cell, isPersistent)
    {
        IsHLOD = cell.GetOrDefault("bIsHLOD", false);
        DataLayers = cell.DataLayers?.DataLayers.Select(x => x.Text).ToArray() ?? [];
        IsVisible = DataLayers.Length == 0 || IsPersistent; // a cell in a data layer waits for that layer to be turned on

        if (cell.RuntimeCellData?.TryLoad<UWorldPartitionRuntimeCellData>(out var data) == true)
        {
            Is2D = data is UWorldPartitionRuntimeCellDataHashSet { bIs2D: true };

            FVector center;
            FVector extents;
            if (data is UWorldPartitionRuntimeCellDataSpatialHash spatial && spatial.Position != FVector.ZeroVector)
            {
                center = spatial.Position * Settings.GlobalScale;
                extents = new FVector(spatial.Extent * Settings.GlobalScale);
            }
            else
            {
                var box = data.ContentBounds * Settings.GlobalScale;
                box.GetCenterAndExtents(out center, out extents);
            }

            _loadingExtents = new Vector3(extents.X, extents.Z, extents.Y);

            // TODO: not clean
            if (DataLayers.Length > 0)
            {
                var hue = DataLayers.Aggregate(0f, (current1, dl) => dl.Aggregate(current1, (current, c) => current + c));
                hue = (hue * 0.618033988749895f) % 1f;
                var h = hue * 6;
                var x = 1 - MathF.Abs(h % 2 - 1);
                color = h switch
                {
                    < 1 => new Vector3(1, x, 0),
                    < 2 => new Vector3(x, 1, 0),
                    < 3 => new Vector3(0, 1, x),
                    < 4 => new Vector3(0, x, 1),
                    < 5 => new Vector3(x, 0, 1),
                    _ => new Vector3(1, 0, x)
                } * 0.5f;
            }
            else
            {
                color ??= new Vector3(cell.CellDebugColor.R, cell.CellDebugColor.G, cell.CellDebugColor.B);
            }

            Components.Add(new CellRootComponent(new Vector3(center.X, center.Z, center.Y), _loadingExtents.Value, color.Value));
        }

        if (cell is UWorldPartitionRuntimeLevelStreamingCell streaming &&
            streaming.LevelStreaming?.TryLoad<ULevelStreaming>(out var level) == true &&
            level.WorldAsset is { } world)
        {
            _world = world;
        }
    }

    public CellActor(FSoftObjectPath worldAsset, UWorld world, bool isPersistent = false) : base(world, isPersistent)
    {
        Components.Add(new SpatialComponent(null, "CellRoot"));
        DataLayers = [];
        _world = worldAsset;
    }

    protected override CullingBounds? LoadingBounds
    {
        get
        {
            if (_loadingExtents is not { } extents || RootComponent is not { } root) return null;

            var matrix = root.GetLocalTransform().ToMatrix();
            for (var relation = root.Relation; relation != null; relation = relation.Relation)
            {
                matrix *= relation.GetLocalTransform().ToMatrix();
            }

            return new CullingBounds(matrix.Translation, extents);
        }
    }

    protected override bool CanBuild => _world is not null;

    protected internal override Actor Build()
    {
        if (_world is not { } world)
            throw new InvalidOperationException($"{Name} has no world to build.");

        var actor = new WorldActor(world.Load<UWorld>() ?? throw new InvalidOperationException($"{Name} world asset could not be loaded."));
        if (actor.RootComponent != null && RootComponent != null)
        {
            actor.RootComponent.SetLocalTransform(RootComponent.GetLocalTransform().Inverse());
        }

        return actor;
    }

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.PropertyValueTable("Cell", () =>
        {
            EditorUI.Text("State", State.ToString());
            EditorUI.Text("HLOD", IsHLOD ? "Yes" : "No");
            EditorUI.Text("Range Test", $"{(Is2D ? 2 : 3)}D");
            EditorUI.Text("Data Layers", DataLayers.Length == 0 ? "None" : string.Join(", ", DataLayers));
        });
    }

    public override string Icon => State switch
    {
        EStreamingState.Loaded => IsHLOD ? Settings.CityIcon : Settings.CubesIcon,
        EStreamingState.Loading => Settings.SpinnerIcon,
        EStreamingState.Failed => Settings.TriangleExclamationIcon,
        _ => IsHLOD ? Settings.DiceD6Icon : Settings.CubeIcon
    };

    private sealed class CellRootComponent(Vector3 center, Vector3 extents, Vector3 color) : SpatialComponent(new Transform(center), "CellRoot")
    {
        protected override DebugComponent CreateDebugVisualization() => new BoxComponent(extents, color, 5.0f, name: "Cell (Bounds)");
    }
}
