using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LevelActor : Actor
{
    public LevelActor(UObject actor, Dictionary<FPackageIndex, ActorComponent> components) : base(actor.Name)
    {
        var root = actor.GetOrDefault<FPackageIndex?>("RootComponent");
        if (root != null)
        {
            var component = CreateComponent(root);
            _parent = component.Item1;
            
            Components.Add(component.Item2);
            components.TryAdd(root, component.Item2);
        }
        
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));
        
        if (actor.TryGetValue(out UWorld[] additionalWorlds, "AdditionalWorlds"))
        {
            foreach (var additionalWorld in additionalWorlds)
            {
                Children.Add(new WorldActor(additionalWorld, WorldActorType.Components));
            }
        }
    }

    public FPackageIndex? ProcessEnqueuedComponents(Dictionary<FPackageIndex, ActorComponent> components)
    {
        foreach (var ptr in _ptrs)
        {
            CreateRecursive(ptr);
        }
        
        _ptrs.Clear();
        return _parent;
        
        void CreateRecursive(FPackageIndex ptr)
        {
            var component = CreateComponent(ptr);
            if (component is { Item1: not null, Item2: SpatialComponent spatial })
            {
                if (components.TryGetValue(component.Item1, out var parent))
                {
                    if (parent is SpatialComponent parentSpatial)
                    {
                        spatial.Relation = parentSpatial;
                    }
                    else
                    {
                        throw new Exception("Parent component is not a spatial component");
                    }
                }
                else
                {
                    CreateRecursive(component.Item1);
                    // throw new Exception("Parent component not found");
                }
            }
            
            components.TryAdd(ptr, component.Item2);
            Components.Add(component.Item2);
        }
    }
    
    private (FPackageIndex?, ActorComponent) CreateComponent(FPackageIndex ptr)
    {
        FPackageIndex? parent = null;
        ActorComponent component;
        switch (ptr.Load())
        {
            case USceneComponent sceneComponent:
            {
                parent = sceneComponent.GetOrDefault<FPackageIndex?>("AttachParent");
                
                var transform = sceneComponent.GetRelativeTransform();
                switch (sceneComponent)
                {
                    case UStaticMeshComponent staticMeshComponent when staticMeshComponent.GetStaticMesh().TryLoad<UStaticMesh>(out var staticMesh):
                    {
                        if (!staticMesh.TryConvert(out var mesh))
                            throw new ArgumentException("Failed to convert static mesh.", nameof(staticMesh));
                        if (staticMesh.RenderData?.Bounds is null)
                            throw new ArgumentException("Static mesh does not have render data or bounds.", nameof(staticMesh));

                        staticMesh.OverrideMaterials(staticMeshComponent.GetOrDefault<FPackageIndex[]>("OverrideMaterials", []));
                        using (mesh)
                        {
                            if (staticMeshComponent is UInstancedStaticMeshComponent instancedComponent)
                            {
                                component = new InstancedStaticMeshComponent(staticMesh, mesh, transform, instancedComponent.GetInstances());
                            }
                            else
                            {
                                component = new StaticMeshComponent(staticMesh, mesh);
                            }
                        }
                        break;
                    }
                    case USkeletalMeshComponent skeletalMeshComponent when skeletalMeshComponent.GetSkeletalMesh().TryLoad<USkeletalMesh>(out var skeletalMesh):
                    {
                        if (!skeletalMesh.TryConvert(out var mesh))
                            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

                        using (mesh) component = new SkeletalMeshComponent(skeletalMesh, mesh);
                        break;
                    }
                    default:
                    {
                        component = new SpatialComponent(null, $"{sceneComponent.Name} ({sceneComponent.GetType().Name})");
                        // component = new Components.PrimitiveComponent(new Primitives.Cube());
                        break;
                    }
                }

                if (component is SpatialComponent spatial and not InstancedStaticMeshComponent)
                {
                    // instance components already have the correct transform set
                    spatial.LocalTransform = transform;
                }
                break;
            }
            default:
            {
                component = new Components.PrimitiveComponent(new Primitives.Cube());
                break;
            }
        }

        return (parent, component);
    }
    
    private readonly FPackageIndex? _parent;
    private readonly HashSet<FPackageIndex> _ptrs = [];
    private void EnqueuePointers(params FPackageIndex?[] ptrs)
    {
        foreach (var ptr in ptrs)
        {
            if (ptr != null)
            {
                _ptrs.Add(ptr);
            }
        }
    }
}