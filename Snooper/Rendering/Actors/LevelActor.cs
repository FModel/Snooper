using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.SplineMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Component.TextRender;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Extensions;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LevelActor : Actor
{
    private readonly FPackageIndex?[] _textureData;

    public LevelActor(UObject actor, Dictionary<FPackageIndex, SpatialComponent> components, WorldActorType type) : base(actor)
    {
        var compoments = type.Includes(WorldActorType.Components);
        var landscape = type.Includes(WorldActorType.Landscape);
        var additional = type.Includes(WorldActorType.AdditionalWorlds);

        if (compoments)
        {
            EnqueuePointers(actor.GetOrDefault<FPackageIndex?>("RootComponent"));
            EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", []));
            EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));

            actor.TryGetAllValues(out _textureData, "TextureData");
        }

        if (landscape)
        {
            EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("LandscapeComponents", []));
        }

        // ProcessRootComponent
        foreach (var ptr in _ptrs)
        {
            _parent = CreateComponentRecursive(components, ptr).ParentPtr;
            _ptrs.Remove(ptr);
            break;
        }

        if (additional && actor.TryGetValue(out FSoftObjectPath[] additionalWorlds, "AdditionalWorlds"))
        {
            foreach (var additionalWorld in additionalWorlds)
            {
                if (!additionalWorld.TryLoad<UWorld>(out var w)) continue;
                Children.Add(new WorldActor(w, WorldActorType.Components));
            }
        }
    }

    public FPackageIndex? ProcessEnqueuedComponents(Dictionary<FPackageIndex, SpatialComponent> components)
    {
        foreach (var ptr in _ptrs)
        {
            CreateComponentRecursive(components, ptr);
        }

        _ptrs.Clear();
        return _parent;
    }

    private Pair CreateComponentRecursive(Dictionary<FPackageIndex, SpatialComponent> components, FPackageIndex ptr)
    {
        Pair root;

        var pair = root = CreateComponent(ptr);
        if (pair.ParentPtr is { IsNull: false })
        {
            if (!components.ContainsKey(pair.ParentPtr))
                root = CreateComponentRecursive(components, pair.ParentPtr);

            pair.Component.Relation = components[pair.ParentPtr];
        }

        components.TryAdd(ptr, pair.Component);
        Components.Add(pair.Component);
        return root;
    }

    private Pair CreateComponent(FPackageIndex ptr)
    {
        FPackageIndex? parent = null;
        SpatialComponent component;

        var data = ptr.Load();
        switch (data)
        {
            case USceneComponent sceneComponent:
            {
                parent = sceneComponent.GetOrDefault<FPackageIndex?>("AttachParent");

                component = sceneComponent switch
                {
                    UStaticMeshComponent sm when sm.GetStaticMesh().TryLoad<UStaticMesh>(out var mesh) => sm switch
                    {
                        UInstancedStaticMeshComponent ism => new InstancedStaticMeshComponent(mesh, ism),
                        USplineMeshComponent spline => new SplineMeshComponent(mesh, spline),
                        _ => new StaticMeshComponent(mesh, sm)
                    },
                    USkeletalMeshComponent sk when sk.GetSkeletalMesh().TryLoad<USkeletalMesh>(out var mesh) => new SkeletalMeshComponent(mesh, sk),
                    ULandscapeComponent landscapeComponent => new LandscapeMeshComponent(landscapeComponent),
                    UBillboardComponent billboardComponent => new BillboardComponent(billboardComponent),
                    UBrushComponent brushComponent when brushComponent.GetBrush() is { } brush => new BrushComponent(brushComponent, brush),
                    UShapeComponent { Outer: not ALevelBounds } shape => shape switch // exclude level bounds because their scale looks weird and overall they provide little value
                    {
                        UBoxComponent boxComponent => new BoxComponent(boxComponent),
                        USphereComponent sphereComponent => new SphereComponent(sphereComponent),
                        UCapsuleComponent capsuleComponent => new CapsuleComponent(capsuleComponent),
                        _ => new SpatialComponent(shape)
                    },
                    ULightComponentBase light => light switch
                    {
                        USpotLightComponent spotLightComponent => new SpotLightComponent(spotLightComponent),
                        UPointLightComponent pointLightComponent => new PointLightComponent(pointLightComponent),
                        URectLightComponent rectLightComponent => new RectLightComponent(rectLightComponent),
                        UDirectionalLightComponent directionalLightComponent => new DirectionalLightComponent(directionalLightComponent),
                        USkyLightComponent skyLightComponent => new SkyLightComponent(skyLightComponent),
                        _ => new LightComponent(light)
                    },
                    UAudioComponent audioComponent => new AudioComponent(audioComponent),
                    UTextRenderComponent textComponent => new TextRenderComponent(textComponent),
                    _ => new SpatialComponent(sceneComponent)
                };

                if (component is StaticMeshComponent staticMeshComponent)
                {
                    for (var i = 0; i < _textureData.Length; i++)
                    {
                        var dataPtr = _textureData[i];
                        if (dataPtr == null || dataPtr.IsNull || !dataPtr.TryLoad<UBuildingTextureData>(out var textureData))
                            continue;

                        staticMeshComponent.RegisterTextureData(textureData, i);
                    }
                }
                break;
            }
            case UActorComponent actorComponent:
            {
                component = new SpatialComponent(actorComponent);
                break;
            }
            default: // uobject
            {
                component = new SpatialComponent(null, $"{data?.Name} ({data?.GetType().Name})");
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
