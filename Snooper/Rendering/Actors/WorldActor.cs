using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
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
    WorldPartition    = 1 << 3,
    LevelStreaming    = 1 << 4,
    AdditionalWorlds  = 1 << 5,

    BaseResolution    = Components | Landscape | AdditionalWorlds, // loads whatever components this world has, including landscape but excluding world partition and level streaming
    HighResolution    = Landscape | WorldPartition | LevelStreaming, // loads only landscape from this world and parse partition and level streaming at BaseResolution
}

public class WorldActor : Actor
{
    public WorldActor(UWorld world, TransformComponent? transform = null, WorldActorType type = WorldActorType.BaseResolution) : base(world.Name, transform: transform)
    {
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
        
        var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
        foreach (var ptr in actors)
        {
            if (ptr == null || !ptr.TryLoad(out UObject actor))
                continue;
            
            var root = actor.GetOrDefault<FPackageIndex?>("RootComponent");
            var scene = root?.Load<USceneComponent>();
            if (landscape && actor is ALandscapeProxy proxy)
            {
                Children.Add(new LandscapeProxyActor(proxy, scene?.GetRelativeTransform()));
                continue;
            }

            if (partition)
            {
                Process(actor.GetOrDefault<FPackageIndex?>("WorldPartition"));
                continue;
            }

            if (compoments)
            {
                // am I crazy or InstanceComponents[0] may or may not be the root component?
                CreateActor(root);
                CreateActor(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", []));
                CreateActor(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));

                if (actor is AInstancedFoliageActor foliage)
                {
                    foreach (var info in foliage.FoliageInfos ?? [])
                    {
                        if (info.Value.Implementation is not FFoliageStaticMesh staticMesh)
                            continue;
                        
                        CreateActor(staticMesh.Component);
                    }
                }
            }
            
            if (additional && actor.TryGetValue(out UWorld[] additionalWorlds, "AdditionalWorlds"))
            {
                // this is a visual hack to add additional worlds to the scene
                // technically additional worlds are children of the root component
                foreach (var additionalWorld in additionalWorlds)
                {
                    Children.Add(new WorldActor(additionalWorld, scene?.GetRelativeTransform(), WorldActorType.Components));
                }
            }
        }
        
        _parents.Clear();
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

    private void CreateActor(FPackageIndex?[] ptrs)
    {
        foreach (var ptr in ptrs)
        {
            CreateActor(ptr);
        }
    }

    private Actor? CreateActor(FPackageIndex? ptr)
    {
        if (ptr == null || !ptr.TryLoad(out UActorComponent component) || component.ExportType == "ShadowProxyMeshComponent_C")
            return null;
        
        if (_parents.TryGetValue(ptr, out var existing))
            return existing;

        Actor a;
        Actor? parent = null;
        if (component is USceneComponent sceneComponent)
        {
            parent = CreateActor(sceneComponent.GetOrDefault<FPackageIndex?>("AttachParent"));

            var transform = sceneComponent.GetRelativeTransform();
            if (component is UStaticMeshComponent staticMeshComponent && staticMeshComponent.GetStaticMesh().TryLoad(out UStaticMesh staticMesh))
            {
                if (staticMesh.Name.EndsWith("_SingleCluster")) return null;
                
                staticMesh.OverrideMaterials(staticMeshComponent.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
                
                if (component is UInstancedStaticMeshComponent { PerInstanceSMData.Length: > 0 } instancedComponent)
                {
                    // act as a container for instances
                    // downside, a container will never be instanced itself, so we may miss out on some performance if 2 containers instance the same mesh
                    // upside, instances relation is properly handled, OverrideMaterials are applied to the correct instances
                    a = new Actor(component.Name, transform: transform);
                    a.Children.Add(new MeshActor(staticMesh, instancedComponent.PerInstanceSMData));
                }
                else
                {
                    a = new MeshActor(staticMesh, transform);
                }
            }
            else if (component is USkeletalMeshComponent skeletalMeshComponent && skeletalMeshComponent.GetSkeletalMesh().TryLoad(out USkeletalMesh skeletalMesh))
            {
                a = new MeshActor(skeletalMesh, transform);
            }
            else
            {
                a = new Actor($"{component.Name} ({component.GetType().Name})", transform: transform);
                // a.Components.Add(new Components.PrimitiveComponent(new Primitives.Cube()));
            }
        }
        else
        {
            a = new Actor($"{component.Name} ({component.GetType().Name})");
        }
        
        parent ??= this;
        parent.Children.Add(a);

        _parents.Add(ptr, a);
        return a;
    }

    private readonly Dictionary<FPackageIndex, Actor> _parents = [];
}