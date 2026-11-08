using System.Numerics;
using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Assets.Exports.WorldPartition.DataLayer;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
using Snooper.Rendering.Components.Transforms;
using ImGuiNET;

namespace Snooper.Rendering.Actors;

public class WorldActor : Actor
{
    private readonly Dictionary<string, string> _dataLayers = new();
    private readonly HashSet<string> _visibleDataLayers = [];

    public WorldActor(UWorld world, IProgress<string>? progress = null, CancellationToken ct = default) : base(world)
    {
        Components.Add(new SpatialComponent(null, "WorldRoot"));

        var level = world.PersistentLevel.Load<ULevel>();
        if (level == null) return;

        ProcessDataLayers();
        ProcessWorldPartition();
        ProcessWorldComposition();

        var actors = new Dictionary<ResolvedObject, LevelActor>();
        var length = level.Actors.Length;
        for (var i = 0; i < length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (level.Actors[i] is not { } ptr || ptr.IsNull || !ptr.TryLoad<UObject>(out var data))
            {
                continue;
            }

            progress?.Report($"{world.Name} ... Actors {i + 1}/{length}");
            actors.TryAdd(ptr.ResolvedObject!, new LevelActor(data));
        }
        length = actors.Count;

        if (level.Owner is { } package)
        {
            // an actor owns every component outered to it
            for (var i = 0; i < package.ExportMapLength; i++)
            {
                var export = new FPackageIndex(package, i + 1);
                if (package.ResolvePackageIndex(export) is { Outer: { } outer } resolved && actors.TryGetValue(outer, out var owner) &&
                    resolved.GetObjectType() is { } type && type.IsAssignableTo(typeof(UActorComponent)))
                {
                    owner.EnqueuePointers(export);
                }
            }
        }

        var processed = 0;
        var components = new Dictionary<ResolvedObject, SpatialComponent>();
        foreach (var actor in actors.Values)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"{world.Name} ... Actor Components {++processed}/{length}");

            actor.ProcessEnqueuedComponents(components, actors);
        }
        components.Clear();

        progress?.Report($"{world.Name} ... Resolving Actor Hierarchy");
        foreach (var actor in actors.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (actor.RootComponent is not { } root) continue;

            (root.Relation?.Actor ?? this).Children.Add(actor);
        }
        actors.Clear();

        length = world.StreamingLevels?.Length ?? 0;
        for (var i = 0; i < length; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"{world.Name} ... Streaming Levels {i + 1}/{length}");
            Process(world.StreamingLevels![i]); // TODO: these fucking streaming levels can reference each other
        }

        void ProcessDataLayers()
        {
            if (!level.WorldDataLayers.TryLoad<AWorldDataLayers>(out var worldDataLayers)) return;

            progress?.Report($"{world.Name} ... Processing Data Layers");
            foreach (var instancePtr in worldDataLayers.DataLayerInstances)
            {
                if (!instancePtr.TryLoad<UDataLayerInstance>(out var instance)) continue;

                switch (instance)
                {
                    case UDataLayerInstanceWithAsset withAsset when withAsset.DataLayerAsset.TryLoad<UDataLayerAsset>(out var dataLayer):
                        _dataLayers[withAsset.Name] = dataLayer.Name;
                        if (withAsset.InitialRuntimeState is EDataLayerRuntimeState.Activated)
                            _visibleDataLayers.Add(withAsset.Name);
                        break;
                }
            }
        }
        void ProcessWorldPartition()
        {
            if (!level.WorldSettings.TryLoad<AWorldSettings>(out var worldSettings) ||
                worldSettings.WorldPartition == null || !worldSettings.WorldPartition.TryLoad<UWorldPartition>(out var worldPartition))
                return;

            progress?.Report($"{world.Name} ... Processing World Partition");
            Children.Add(new PartitionActor(worldPartition, _visibleDataLayers));
        }
        void ProcessWorldComposition()
        {
            if (world.WorldComposition is not { } ptr ||
                !ptr.TryLoad<UWorldComposition>(out var composition) || composition.Tiles is not { Length: > 0 })
                return;

            progress?.Report($"{world.Name} ... Processing World Composition");
            Children.Add(new PartitionActor(composition));
        }
    }

    private void Process(FPackageIndex? ptr)
    {
        switch (ptr?.Load())
        {
            case UWorldPartition partition:
            {
                Process(partition.RuntimeHash); // UWorldPartitionRuntimeHash
                break;
            }
            case UWorldPartitionRuntimeHashSet set:
            {
                var hlod = set.RuntimeStreamingData.OrderBy(x => x.LoadingRange).ElementAt(1);
                for (var i = 0; i < hlod.SpatiallyLoadedCells.Length; i++)
                {
                    Process(hlod.SpatiallyLoadedCells[i]); // UWorldPartitionRuntimeLevelStreamingCell
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial when spatial.StreamingGrids[0].GridLevels.Length > 0:
            {
                for (var i = 0; i < spatial.StreamingGrids[0].GridLevels[0].LayerCells.Length; i++)
                {
                    Process(spatial.StreamingGrids[0].GridLevels[0].LayerCells[i].GridCells[0]); // UWorldPartitionRuntimeLevelStreamingCell
                }
                break;
            }
            case UWorldPartitionRuntimeLevelStreamingCell cell:
            {
                Process(cell.LevelStreaming); // UWorldPartitionLevelStreamingDynamic
                break;
            }
            case ULevelStreaming { WorldAsset: { } worldAsset } streaming when worldAsset.TryLoad<UWorld>(out var world):
            {
                Children.Add(new CellActor(worldAsset, world, streaming is ULevelStreamingAlwaysLoaded or ULevelStreamingPersistent, streaming.LevelTransform));
                break;
            }
        }
    }

    public override void Export(ExportSession session, CancellationToken ct = default)
    {
        if (ActorManager is not { } manager || string.IsNullOrEmpty(Path))
            return;

        try
        {
            session.Add(manager.FileProvider.LoadPackageObject(Path, Name));
        }
        catch (Exception e)
        {
            Log.Warning(e, "{World} could not be queued for export", Name);
        }

        ExportLoaded(this);

        void ExportLoaded(Actor actor)
        {
            foreach (var child in actor.Children)
            {
                ct.ThrowIfCancellationRequested();
                if (child is StreamableActor { IsLoaded: true }) child.Export(session, ct);
                else ExportLoaded(child);
            }
        }
    }

    public override string Icon => Settings.EarthEuropeIcon;

    public override void DrawControls()
    {
        base.DrawControls();

        if (_dataLayers.Count == 0)
        {
            ImGui.TextUnformatted("No data layers found");
            return;
        }

        if (ImGui.BeginListBox("##DataLayers", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
        {
            var partition = Children.OfType<PartitionActor>().FirstOrDefault();
            if (partition != null)
            {
                foreach (var (instanceName, layerName) in _dataLayers)
                {
                    var isVisible = _visibleDataLayers.Contains(instanceName);
                    if (ImGui.Checkbox(layerName, ref isVisible))
                    {
                        if (isVisible)
                        {
                            _visibleDataLayers.Add(instanceName);
                        }
                        else
                        {
                            _visibleDataLayers.Remove(instanceName);
                        }

                        partition.SetDataLayerEnabled(instanceName, isVisible);
                    }
                }
            }
            ImGui.EndListBox();
        }
    }
}
