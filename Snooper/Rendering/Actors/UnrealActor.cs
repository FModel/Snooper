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
using Snooper.Rendering.Components.Collection;
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
            case USceneComponent scene:
            {
                parent = scene.GetOrDefault<FPackageIndex?>("AttachParent");

                component = scene switch
                {
                    // Get.*Mesh() is not being used because it ignores null fields, null meaning discard the mesh for this component (ig?)
                    UStaticMeshComponent sm when sm.TryGetValue<UStaticMesh>(out var mesh, "StaticMesh") => sm switch
                    {
                        UInstancedStaticMeshComponent ism => new InstancedStaticMeshComponent(mesh, ism),
                        USplineMeshComponent spline => new SplineMeshComponent(mesh, spline),
                        _ => new StaticMeshComponent(mesh, sm)
                    },
                    USkeletalMeshComponent sk when sk.TryGetValue<USkeletalMesh>(out var mesh, "SkeletalMesh", "SkinnedAsset") => new SkeletalMeshComponent(mesh, sk),
                    UGeometryCollectionComponent gc => new GeometryCollectionComponent(gc),
                    ULandscapeComponent landscape => new LandscapeMeshComponent(landscape),
                    ULandscapeSplinesComponent splines => new LandscapeSplinesComponent(splines),
                    UDecalComponent decal => new DecalComponent(decal),
                    UBillboardComponent billboard => new BillboardComponent(billboard),
                    UArrowComponent arrow => new ArrowComponent(arrow),
                    UBrushComponent brushComponent when brushComponent.GetBrush() is { } brush => new BrushComponent(brushComponent, brush),
                    UShapeComponent shape when shape.Outer?.Object?.Value is not ALevelBounds => shape switch // exclude level bounds because their scale looks weird and overall they provide little value
                    {
                        UBoxComponent box => new BoxComponent(box),
                        USphereComponent sphere => new SphereComponent(sphere),
                        UCapsuleComponent capsule => new CapsuleComponent(capsule),
                        _ => new SpatialComponent(shape)
                    },
                    ULightComponentBase light => light switch
                    {
                        USpotLightComponent spotLight => new SpotLightComponent(spotLight),
                        UPointLightComponent pointLight => new PointLightComponent(pointLight),
                        URectLightComponent rectLight => new RectLightComponent(rectLight),
                        UDirectionalLightComponent directionalLight => new DirectionalLightComponent(directionalLight),
                        _ => new SpatialComponent(light)
                    },
                    UAudioComponent audio => new AudioComponent(audio),
                    UTextRenderComponent text => new TextRenderComponent(text),
                    UCameraComponent camera => new CameraComponent(camera),
                    _ => new SpatialComponent(scene)
                };
                break;
            }
            case UActorComponent actor:
            {
                component = new SpatialComponent(actor);
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
