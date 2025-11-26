using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Extensions;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

[Flags]
public enum WorldActorType
{
    Components        = 1 << 1,
    Landscape         = 1 << 2,
    LevelStreaming    = 1 << 3,
    AdditionalWorlds  = 1 << 4,

    BaseResolution    = Components | Landscape | AdditionalWorlds, // loads whatever components this world has, including landscape but excluding world partition and level streaming
    HighResolution    = Landscape | LevelStreaming, // loads only landscape from this world and parse partition and level streaming at BaseResolution
}

public class WorldActor : Actor
{
    public WorldActor(UWorld world, WorldActorType type = WorldActorType.BaseResolution) : base(world)
    {
        Components.Add(new SpatialComponent(null, "WorldRoot"));

        var level = world.PersistentLevel.Load<ULevel>();
        if (level == null) return;

        if (level.WorldSettings.TryLoad<AWorldSettings>(out var settings) &&
            settings.WorldPartition.TryLoad<UWorldPartition>(out var partition))
        {
            Children.Add(new PartitionActor(partition));
        }

        var parents = new Dictionary<FPackageIndex, SpatialComponent>();
        var created = new List<LevelActor>();
        foreach (var ptr in level.Actors)
        {
            if (ptr == null || !ptr.TryLoad<UObject>(out var data))
            {
                continue;
            }

            var a = new LevelActor(data, parents, type);
            if (a.RootComponent is not null)
            {
                created.Add(a);
            }
        }

        foreach (var actor in created)
        {
            var parent = actor.ProcessEnqueuedComponents(parents);
            if (parent != null)
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

        if (type.Includes(WorldActorType.LevelStreaming))
        {
            for (var i = 0; i < world.StreamingLevels.Length; i++)
            {
                Process(world.StreamingLevels[i]);
                if (i > 5) break; // TODO: optimize
            }
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
            case ULevelStreaming { WorldAsset: { } world }:
            {
                Children.Add(new WorldActor(world.Load<UWorld>()));
                break;
            }
        }
    }
}
