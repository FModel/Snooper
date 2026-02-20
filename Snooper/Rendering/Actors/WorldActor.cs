using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Assets.Exports.WorldPartition.DataLayer;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;
using ImGuiNET;

namespace Snooper.Rendering.Actors;

public class WorldActor : Actor
{
    private readonly Dictionary<string, string> _dataLayers = new();
    private readonly HashSet<string> _visibleDataLayers = [];

    public WorldActor(UWorld world) : base(world)
    {
        Components.Add(new SpatialComponent(null, "WorldRoot"));

        var level = world.PersistentLevel.Load<ULevel>();
        if (level == null) return;

        ProcessDataLayers(level);
        ProcessWorldPartition(level);

        var parents = new Dictionary<FPackageIndex, SpatialComponent>();
        var created = new List<LevelActor>();
        foreach (var ptr in level.Actors)
        {
            if (ptr == null || !ptr.TryLoad<UObject>(out var data))
            {
                continue;
            }

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

        // Parallel.ForEach(world.StreamingLevels, new ParallelOptions { MaxDegreeOfParallelism = 10 }, Process);
        for (var i = 0; i < world.StreamingLevels.Length; i++)
        {
            Process(world.StreamingLevels[i]);
            if (i > 5) break; // TODO: GTA optimize, actually stream it (see world partition) or limit the amount of streaming levels to process
        }

        GC.Collect();
    }

    private void ProcessDataLayers(ULevel level)
    {
        if (!level.WorldDataLayers.TryLoad<AWorldDataLayers>(out var worldDataLayers)) return;

        foreach (var instancePtr in worldDataLayers.DataLayerInstances)
        {
            if (!instancePtr.TryLoad<UDataLayerInstance>(out var instance)) continue;

            switch (instance)
            {
                case UDataLayerInstanceWithAsset withAsset when withAsset.DataLayerAsset.TryLoad<UDataLayerAsset>(out var dataLayer):
                    _dataLayers[withAsset.Name] = dataLayer.Name;
                    break;
            }
        }
    }

    private void ProcessWorldPartition(ULevel level)
    {
        if (!level.WorldSettings.TryLoad<AWorldSettings>(out var worldSettings) ||
            !worldSettings.WorldPartition.TryLoad<UWorldPartition>(out var worldPartition))
            return;

        Children.Add(new PartitionActor(worldPartition));
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
                    if (i > 150) break; // TODO: optimize
                }
                break;
            }
            case UWorldPartitionRuntimeSpatialHash spatial when spatial.StreamingGrids[0].GridLevels.Length > 0:
            {
                for (var i = 0; i < spatial.StreamingGrids[0].GridLevels[0].LayerCells.Length; i++)
                {
                    Process(spatial.StreamingGrids[0].GridLevels[0].LayerCells[i].GridCells[0]); // UWorldPartitionRuntimeLevelStreamingCell
                    if (i > 50) break; // TODO: optimize
                }
                break;
            }
            case UWorldPartitionRuntimeLevelStreamingCell cell:
            {
                Process(cell.LevelStreaming); // UWorldPartitionLevelStreamingDynamic
                break;
            }
            case ULevelStreaming { WorldAsset: { } asset } when asset.TryLoad<UWorld>(out var world):
            {
                Children.Add(new WorldActor(world));
                break;
            }
        }
    }

    internal override void DrawInterface()
    {
        base.DrawInterface();

        ImGui.SeparatorText("Data Layers");
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

                        partition.SetVisibilityByDataLayer(instanceName, isVisible);
                    }
                }
            }
            ImGui.EndListBox();
        }
    }
}
