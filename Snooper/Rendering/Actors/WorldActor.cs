using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.WorldPartition;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Extensions;

namespace Snooper.Rendering.Actors;

[Flags]
public enum WorldActorType
{
    Components        = 1 << 1,
    Landscape         = 1 << 2,
    WorldPartition    = 1 << 3,
    LevelStreaming    = 1 << 4,
    AdditionalWorlds  = 1 << 5,

    BaseResolution    = Components | Landscape | AdditionalWorlds, // loads whatever components this world has, including landscape but excluding world partition and level streaming
    HighResolution    = Landscape | WorldPartition | LevelStreaming, // loads only landscape from this world and parse partition and level streaming at BaseResolution
}

public class WorldActor : Actor
{
    public WorldActor(UWorld world, WorldActorType type = WorldActorType.BaseResolution) : base(world.Name)
    {
        Components.Add(new Components.PrimitiveComponent(new Primitives.Cube()));
        
        var compoments = type.Includes(WorldActorType.Components);
        var landscape = type.Includes(WorldActorType.Landscape);
        var partition = type.Includes(WorldActorType.WorldPartition);
        var streaming = type.Includes(WorldActorType.LevelStreaming);
        var additional = type.Includes(WorldActorType.AdditionalWorlds);

        for (var i = 0; streaming && i < world.StreamingLevels.Length; i++)
        {
            Process(world.StreamingLevels[i]);
            if (i > 5) break; // TODO: optimize
        }

        var created = new List<LevelActor>();
        var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
        foreach (var ptr in actors)
        {
            if (ptr == null || !ptr.TryLoad<UObject>(out var data))
                continue;

            created.Add(new LevelActor(data, _parents));
        }

        foreach (var actor in created)
        {
            var parent = actor.ProcessEnqueuedComponents(_parents);
            if (parent != null)
            {
                if (_parents.TryGetValue(parent, out var root))
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
        _parents.Clear();
    }

    private readonly Dictionary<FPackageIndex, ActorComponent> _parents = [];

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