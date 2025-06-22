using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Snooper;
using Snooper.Rendering;
using Snooper.Rendering.Actors;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}]: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Literate)
    .CreateLogger();

var version = new VersionContainer(EGame.GAME_Valorant, ETexturePlatform.DesktopMobile);
var provider = new DefaultFileProvider("D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks", SearchOption.TopDirectoryOnly, version);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey("0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6"));
provider.PostMount();

var snooper = new SnooperWindow(144, 1500, 900, false);
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new FTransform(new FVector(0, 200, 0)));
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new FTransform(new FVector(0, -200, 0)));
// snooper.AddToScene(provider.LoadPackageObject("Engine/Content/BasicShapes/Cube.Cube"), new FTransform(new FVector(200, 0, 0)));
// snooper.Run();
// return;

var dictionary = new Dictionary<FGuid, MeshActor>();
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_A.Bonsai_Art_A"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_AtkPathA.Bonsai_Art_AtkPathA"));
AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_AtkPathB.Bonsai_Art_AtkPathB"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_AtkSpawn.Bonsai_Art_AtkSpawn"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_ATower.Bonsai_Art_ATower"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_B.Bonsai_Art_B"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_Birthday.Bonsai_Art_Birthday"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_BTower.Bonsai_Art_BTower"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_DefPathA.Bonsai_Art_DefPathA"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_DefPathB.Bonsai_Art_DefPathB"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_DefSpawn.Bonsai_Art_DefSpawn"));
// AddWorldToScene(provider.LoadPackageObject<UWorld>("ShooterGame/Content/Maps/Bonsai/Bonsai_Art_Mid.Bonsai_Art_Mid"));

snooper.Run();

void AddWorldToScene(UWorld world)
{
    var scene = new Actor(world.ObjectGuid ?? Guid.NewGuid(), world.Name);

    var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
    foreach (var actorPtr in actors)
    {
        if (!actorPtr.TryLoad(out AActor actor)) continue;

        if (actor.TryGetValue(out USceneComponent[] components, "InstanceComponents", "BlueprintCreatedComponents"))
        {
            foreach (var component in components)
            {
                AddToScene(component, scene);
            }
        }
        else if (actor.TryGetValue(out UStaticMeshComponent component, "StaticMeshComponent"))
        {
            AddToScene(component, scene);
        }
    }
    
    snooper.AddToScene(scene);
}

void AddToScene(USceneComponent component, Actor parent)
{
    if (component.GetAttachParent() is UStaticMeshComponent attachParent &&
        attachParent.GetStaticMesh().TryLoad(out UStaticMesh parentStaticMesh))
    {
        AddToScene(attachParent, dictionary[parentStaticMesh.LightingGuid]);
    }
    
    if (component is UStaticMeshComponent staticMeshComponent && staticMeshComponent.GetStaticMesh().TryLoad(out UStaticMesh staticMesh))
    {
        var transform = new FTransform(
            component.GetRelativeRotation(),
            component.GetRelativeLocation(),
            component.GetRelativeScale3D());
        
        if (component is UInstancedStaticMeshComponent { PerInstanceSMData.Length: > 0 } instancedComponent)
        {
            parent.Children.Add(new MeshActor(transform, staticMesh, instancedComponent.PerInstanceSMData));
        }
        else if (dictionary.TryGetValue(staticMesh.LightingGuid, out var actor))
        {
            actor.InstancedTransforms.AddInstance(transform);
        }
        else
        {
            var mesh = new MeshActor(staticMesh, transform);
            dictionary.Add(mesh.Guid, mesh);
            parent.Children.Add(mesh);
        }
    }
}