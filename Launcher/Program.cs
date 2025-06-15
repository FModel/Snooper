using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Snooper;
using Snooper.Rendering;
using Snooper.Rendering.Actors;

var version = new VersionContainer(EGame.GAME_Valorant, ETexturePlatform.DesktopMobile);
var provider = new DefaultFileProvider("D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks", SearchOption.TopDirectoryOnly, version);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey("0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6"));
provider.PostMount();

var snooper = new SnooperWindow(144, 1500, 900, false);
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"));
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"));
// snooper.AddToScene(provider.LoadPackageObject("Engine/Content/BasicShapes/Cube.Cube"), new FTransform(new FVector(500, 0, 0)));
// snooper.Run();
// return;

var dictionary = new Dictionary<FGuid, MeshActor>();
var world = provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_AtkPathB.Bonsai_Art_AtkPathB");
var scene = new Actor(world.ObjectGuid ?? Guid.NewGuid(), world.Name);

var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
foreach (var actorPtr in actors)
{
    if (!actorPtr.TryLoad(out var actor)) continue;

    if (actor.TryGetValue(out UInstancedStaticMeshComponent[] instanceComponents, "InstanceComponents"))
    {
        foreach (var instanceComponent in instanceComponents)
        {
            AddToScene(instanceComponent, scene);
        }
    }
    else if (actor.TryGetValue(out UStaticMeshComponent[] bpComponents, "BlueprintCreatedComponents"))
    {
        foreach (var bpComponent in bpComponents)
        {
            AddToScene(bpComponent, scene);
        }
    }
    else if (actor.TryGetValue(out UStaticMeshComponent staticMeshComponent, "StaticMeshComponent"))
    {
        AddToScene(staticMeshComponent, scene);
    }
}
snooper.AddToScene(scene);

snooper.Run();

void AddToScene(UStaticMeshComponent component, Actor parent)
{
    if (component.TryGetValue(out FPackageIndex attachParent, "AttachParent") &&
        attachParent.TryLoad(out UStaticMeshComponent parentComponent) &&
        parentComponent.GetStaticMesh().TryLoad(out UStaticMesh parentStaticMesh))
    {
        AddToScene(parentComponent, dictionary[parentStaticMesh.LightingGuid]);
    }
    
    if (component.GetStaticMesh().TryLoad(out UStaticMesh staticMesh))
    {
        var transform = new FTransform(
            component.GetOrDefault("RelativeRotation", FRotator.ZeroRotator).Quaternion(),
            component.GetOrDefault("RelativeLocation", FVector.ZeroVector),
            component.GetOrDefault("RelativeScale3D", FVector.OneVector)
        );
        
        if (component is UInstancedStaticMeshComponent instancedComponent)
        {
            foreach (var data in instancedComponent.PerInstanceSMData ?? [])
            {
                parent.Children.Add(new MeshActor(staticMesh, data.TransformData * transform));
            }
        }
        else
        {
            var mesh = new MeshActor(staticMesh, transform);
            dictionary.TryAdd(mesh.Guid, mesh);
            parent.Children.Add(mesh);
        }
    }
}