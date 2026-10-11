using System.Numerics;
using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
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

    public WorldActor(UWorld world, IProgress<string>? progress = null) : base(world)
    {
        Components.Add(new SpatialComponent(null, "WorldRoot"));

        var level = world.PersistentLevel.Load<ULevel>();
        if (level == null) return;

        ProcessDataLayers();
        ProcessWorldPartition();

        var parents = new Dictionary<FPackageIndex, SpatialComponent>();
        var created = new List<LevelActor>();
        var length = level.Actors.Length;
        for (var i = 0; i < length; i++)
        {
            if (level.Actors[i] is not { } ptr || ptr.IsNull || !ptr.TryLoad<UObject>(out var data))
            {
                continue;
            }

            progress?.Report($"{world.Name} ... Actors {i}/{length}");
            var a = new LevelActor(data, parents);
            if (a.RootComponent is not null)
            {
                created.Add(a);
            }
        }

        foreach (var actor in created)
        {
            var parent = actor.ProcessEnqueuedComponents(parents);
            if (parent is { IsNull: false })
            {
                if (parents.TryGetValue(parent, out var root))
                {
                    root.Actor?.Children.Add(actor);
                }
                else
                {
                    throw new Exception("Parent actor not found");
                }
            }
            else
            {
                Children.Add(actor);
            }
        }

        created.Clear();
        parents.Clear();

        length = world.StreamingLevels?.Length ?? 0;
        for (var i = 0; i < length; i++)
        {
            progress?.Report($"{world.Name} ... Streaming Levels {i}/{length}");
            Process(world.StreamingLevels![i]); // TODO: these fucking streaming levels can reference each other
        }

        void ProcessDataLayers()
        {
            if (!level.WorldDataLayers.TryLoad<AWorldDataLayers>(out var worldDataLayers)) return;

            progress?.Report($"{world.Name} ... Data Layers");
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

            progress?.Report($"{world.Name} ... World Partition");
            Children.Add(new PartitionActor(worldPartition, _visibleDataLayers));
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
                Children.Add(new CellActor(worldAsset, world, streaming is ULevelStreamingAlwaysLoaded or ULevelStreamingPersistent));
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
