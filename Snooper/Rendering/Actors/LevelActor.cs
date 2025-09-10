using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
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
    public LevelActor(UObject actor, Dictionary<FPackageIndex, SpatialComponent> components) : base(actor.Name)
    {
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?>("RootComponent"));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("LandscapeComponents", []));

        foreach (var ptr in _ptrs)
        {
            var pair = CreateComponent(ptr);
            _parent = pair.ParentPtr;
            
            Components.Add(pair.Component);
            components.TryAdd(ptr, pair.Component);
            
            _ptrs.Remove(ptr);
            break;
        }
        
        if (actor.TryGetValue(out UWorld[] additionalWorlds, "AdditionalWorlds"))
        {
            foreach (var additionalWorld in additionalWorlds)
            {
                Children.Add(new WorldActor(additionalWorld, WorldActorType.Components));
            }
        }
    }

    public FPackageIndex? ProcessEnqueuedComponents(Dictionary<FPackageIndex, SpatialComponent> components)
    {
        foreach (var ptr in _ptrs)
        {
            CreateRecursive(ptr);
        }
        
        _ptrs.Clear();
        return _parent;
        
        void CreateRecursive(FPackageIndex ptr)
        {
            var pair = CreateComponent(ptr);
            if (pair is { ParentPtr: not null })
            {
                if (!components.ContainsKey(pair.ParentPtr))
                    CreateRecursive(pair.ParentPtr);

                pair.Component.Relation = components[pair.ParentPtr];
            }
            
            components.TryAdd(ptr, pair.Component);
            Components.Add(pair.Component);
        }
    }
    
    private Pair CreateComponent(FPackageIndex ptr)
    {
        FPackageIndex? parent = null;
        SpatialComponent component;

        var data = ptr.Load();
        var name = $"{data?.Name} ({data?.GetType().Name})";
        switch (data)
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
                                component = new InstancedStaticMeshComponent(staticMesh, mesh, instancedComponent.GetInstances(), transform, name);
                            }
                            else
                            {
                                component = new StaticMeshComponent(staticMesh, mesh, transform, name);
                            }
                        }
                        break;
                    }
                    case USkeletalMeshComponent skeletalMeshComponent when skeletalMeshComponent.GetSkeletalMesh().TryLoad<USkeletalMesh>(out var skeletalMesh):
                    {
                        if (!skeletalMesh.TryConvert(out var mesh))
                            throw new ArgumentException("Failed to convert skeletal mesh.", nameof(skeletalMesh));

                        using (mesh) component = new SkeletalMeshComponent(skeletalMesh, mesh, transform, name);
                        break;
                    }
                    case ULandscapeComponent landscapeComponent:
                    {
                        component = new LandscapeMeshComponent(landscapeComponent, transform, name);
                        break;
                    }
                    default:
                    {
                        component = new SpatialComponent(transform, name);
                        // component = new Components.PrimitiveComponent(new Primitives.Cube(), transform, name);
                        break;
                    }
                }
                break;
            }
            default:
            {
                // component = new SpatialComponent(null, name);
                component = new Components.PrimitiveComponent(new Primitives.Cube(), null, name);
                break;
            }
        }

        return new Pair(parent, component);
    }
    
    private readonly FPackageIndex? _parent;
    private readonly HashSet<FPackageIndex> _ptrs = [];
    private void EnqueuePointers(params FPackageIndex?[] ptrs)
    {
        foreach (var ptr in ptrs)
        {
            if (ptr is { IsNull: false })
            {
                _ptrs.Add(ptr);
            }
        }
    }
    
    private readonly struct Pair(FPackageIndex? parentPtr, SpatialComponent component)
    {
        public readonly FPackageIndex? ParentPtr = parentPtr;
        public readonly SpatialComponent Component = component;
    }
}