using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class WorldActor : Actor
{
    public WorldActor(UWorld world, TransformComponent? transform = null) : base(world.Name, transform: transform)
    {
        var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
        foreach (var ptr in actors)
        {
            if (ptr == null || !ptr.TryLoad(out AActor actor))
                continue;
            
            var root = actor.GetOrDefault<FPackageIndex?>("RootComponent");
            var scene = root?.Load<USceneComponent>();
            if (actor is ALandscapeProxy landscape)
            {
                Children.Add(new LandscapeProxyActor(landscape, scene?.GetRelativeTransform()));
                continue;
            }

            // the first instance component is (always?) the root component, this avoids duplicating it
            CreateActor(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", [root]));
            CreateActor(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));
            
            if (actor.TryGetValue(out UWorld[] additionalWorlds, "AdditionalWorlds"))
            {
                // this is a visual hack to add additional worlds to the scene
                // technically additional worlds are children of the root component
                foreach (var additionalWorld in additionalWorlds)
                {
                    Children.Add(new WorldActor(additionalWorld, scene?.GetRelativeTransform()));
                }
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
        if (ptr == null || !ptr.TryLoad(out UActorComponent component))
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
                staticMesh.OverrideMaterials(staticMeshComponent.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
                
                if (component is UInstancedStaticMeshComponent { PerInstanceSMData.Length: > 0 } instancedComponent)
                {
                    // act as a container for instances
                    // downside, a container will never be instanced itself, so we may miss out on some performance if 2 containers instance the same mesh
                    // upside, instances relation is properly handled
                    a = new Actor(component.Name, transform: transform);
                    a.Children.Add(new MeshActor(staticMesh, instancedComponent.PerInstanceSMData));
                }
                else
                {
                    a = new MeshActor(staticMesh, transform);
                }
            }
            else
            {
                a = new Actor($"{component.Name} ({component.GetType().Name})", transform: transform);
                // a.Components.Add(new PrimitiveComponent(new Cube()));
                // actor.Transform.Scale = Vector3.One / 3;
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