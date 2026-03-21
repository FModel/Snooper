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
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Audio;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public abstract class UnrealActor(UObject actor) : Actor(actor)
{
    protected ComponentPair CreateComponentPair(FPackageIndex ptr)
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
                    // Get.*Mesh() is not being used because it ignores null fields, null meaning discard the mesh for this component (ig?)
                    UStaticMeshComponent sm when sm.TryGetValue<UStaticMesh>(out var mesh, "StaticMesh") => sm switch
                    {
                        UInstancedStaticMeshComponent ism => new InstancedStaticMeshComponent(mesh, ism),
                        USplineMeshComponent spline => new SplineMeshComponent(mesh, spline),
                        _ => new StaticMeshComponent(mesh, sm)
                    },
                    USkeletalMeshComponent sk when sk.TryGetValue<USkeletalMesh>(out var mesh, "SkeletalMesh", "SkinnedAsset") => new SkeletalMeshComponent(mesh, sk),
                    ULandscapeComponent landscapeComponent => new LandscapeMeshComponent(landscapeComponent),
                    ULandscapeSplinesComponent landscapeSplinesComponent => new LandscapeSplinesComponent(landscapeSplinesComponent),
                    UBillboardComponent billboardComponent => new BillboardComponent(billboardComponent),
                    UArrowComponent arrowComponent => new ArrowComponent(arrowComponent),
                    UBrushComponent brushComponent when brushComponent.GetBrush() is { } brush => new BrushComponent(brushComponent, brush),
                    UShapeComponent shape when shape.Outer?.Object?.Value is not ALevelBounds => shape switch // exclude level bounds because their scale looks weird and overall they provide little value
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
                        _ => new SpatialComponent(light)
                    },
                    UAudioComponent audioComponent => new AudioComponent(audioComponent),
                    UTextRenderComponent textComponent => new TextRenderComponent(textComponent),
                    UCameraComponent cameraComponent => new CameraComponent(cameraComponent),
                    _ => new SpatialComponent(sceneComponent)
                };
                break;
            }
            case UActorComponent actorComponent:
            {
                component = new SpatialComponent(actorComponent);
                break;
            }
            default: // uobject
            {
                component = new SpatialComponent(name: $"{data?.Name} ({data?.GetType().Name})");
                break;
            }
        }

        return new ComponentPair(parent, component);
    }

    protected readonly struct ComponentPair(FPackageIndex? parentPtr, SpatialComponent component)
    {
        public readonly FPackageIndex? ParentPtr = parentPtr;
        public readonly SpatialComponent Component = component;
    }
}
