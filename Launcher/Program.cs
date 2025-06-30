using System.Numerics;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.Landscape;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
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
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Primitives;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}]: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Literate)
    .CreateLogger();

OodleHelper.Initialize();

#if FN
const string dir = "D:\\Games\\Fortnite\\FortniteGame\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\++Fortnite+Release-36.10-CL-43486998-Windows_oo.usmap";
const string key = "0xA43F7FD912C317930F9AABA5075F0ABCF4EE8A7102582636330BECC449D54560";
var version = new VersionContainer(EGame.GAME_UE5_6);
#elif VL
const string dir = "D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks";
const string key = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
var version = new VersionContainer(EGame.GAME_Valorant);
#endif
var provider = new DefaultFileProvider(dir, SearchOption.TopDirectoryOnly, version)
{
#if FN
        MappingsContainer = new FileUsmapTypeMappingsProvider(mapping)
#endif
};
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey(key));
provider.PostMount();

var snooper = new SnooperWindow(144, 1500, 900, false);
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new FTransform(new FVector(0, 200, 0)));
// snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new FTransform(new FVector(0, -200, 0)));
// snooper.AddToScene(provider.LoadPackageObject("Engine/Content/BasicShapes/Cube.Cube"), new FTransform(new FVector(200, 0, 0)));
// snooper.Run();
// return;

var dictionary = new Dictionary<UActorComponent, Actor>();
switch (provider.ProjectName)
{
    case "ShooterGame":
    {
        var files = provider.Files.Values.Where(x => x is { Directory: "ShooterGame/Content/Maps/Bonsai", Extension: "umap" });
        foreach (var file in files)
        {
            var parts = file.NameWithoutExtension.Split('_');
            if (parts.Length < 2 || parts[1] != "Art" || parts[^1] == "VFX") continue;
    
            AddWorldToScene(provider.LoadPackageObject<UWorld>(file.PathWithoutExtension + "." + file.NameWithoutExtension));
        }
        break;
    }
    case "FortniteGame":
    {
        AddWorldToScene(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain.Hermes_Terrain"));
        break;
    }
}

snooper.Run();

void AddWorldToScene(UWorld world, Actor? parent = null)
{
    var add = parent is null;
    parent ??= new Actor(Guid.NewGuid(), world.Name);
    
    var actors = world.PersistentLevel.Load<ULevel>()?.Actors ?? [];
    foreach (var actorPtr in actors)
    {
        if (!actorPtr.TryLoad(out AActor actor)) continue;

        if (actor is ALandscapeProxy landscape && actor.TryGetValue(out USceneComponent root, "RootComponent"))
        {
            parent.Children.Add(new LandscapeProxyActor(landscape, root.GetRelativeTransform()));
            // parent.Children.Add(new LandscapeProxyActor(landscape, root.GetRelativeTransform(), true));
            // break;
        }
        continue;
        
        if (actor.TryGetValue(out UStaticMeshComponent smComponent, "StaticMeshComponent"))
        {
            AddToScene(smComponent, parent);
        }
        
        if (actor.TryGetValue(out UActorComponent[] components, "BlueprintCreatedComponents", "InstanceComponents"))
        {
            foreach (var component in components)
            {
                AddToScene(component, parent);
            }
        }
        
        if (actor.TryGetValue(out UWorld[] additionalWorlds, "AdditionalWorlds"))
        {
#if RELATIONAL_WORLDS
            var relation = parent.Children[dictionary[smComponent].Guid];
            // TODO:
            // world position is determined by its UStaticMeshComponent which acts as an attachment point
            // the current implementation adds a new actor instance if the actor's parent already has the child we want to add
            // because worlds can share the same UStaticMeshComponent, the world position should be determined by the last instance of the UStaticMeshComponent
            // but it is currently not possible for child actors to have an instanced parent relation
            // ---
            // BTW this is the same problem with landscapes, multiple actors can share the same landscape GUID
            // ---
            // pseudo code:
            // var attach = relation.Transform.WorldMatrix;
            // if (relation.InstancedTransforms.LocalMatrices.Count > 0)
            // {
            //     attach = relation.InstancedTransforms.LocalMatrices[^1] * relation.Transform.Relation.WorldMatrix;
            // }
            
            foreach (var additionalWorld in additionalWorlds)
            {
                AddWorldToScene(additionalWorld, relation);
            }
#else
            var transform = smComponent.GetRelativeTransform();
            foreach (var additionalWorld in additionalWorlds)
            {
                var relation = new Actor(Guid.NewGuid(), additionalWorld.Name, transform);
                AddWorldToScene(additionalWorld, relation);
                snooper.AddToScene(relation);
            }
#endif
        }
    }
    
    if (add) snooper.AddToScene(parent);
}

void AddToScene(UActorComponent? component, Actor parent)
{
    if (component is null) return;
    
    Actor actor;
    if (component is USceneComponent sceneComponent)
    {
        var attach = sceneComponent.GetAttachParent();
        if (attach is not null && dictionary.TryGetValue(attach, out var attachment))
        {
            parent = attachment;
        }
        
        var transform = sceneComponent.GetRelativeTransform();
        if (component is UStaticMeshComponent smComponent && smComponent.GetStaticMesh().TryLoad(out UStaticMesh staticMesh))
        {
            if (component is UInstancedStaticMeshComponent { PerInstanceSMData.Length: > 0 } instancedComponent)
            {
                actor = new MeshActor(transform, staticMesh, instancedComponent.PerInstanceSMData);
            }
            else
            {
                actor = new MeshActor(staticMesh, transform);
            }
        }
        else
        {
            actor = new Actor(Guid.NewGuid(), $"{component.Name} ({component.GetType().Name})", transform);
            actor.Components.Add(new PrimitiveComponent(new Cube()));
            // actor.Transform.Scale = Vector3.One / 3;
        }
    }
    else
    {
        actor = new Actor(Guid.NewGuid(), $"{component.Name} ({component.GetType().Name})");
    }

    dictionary.TryAdd(component, actor);
    parent.Children.Add(actor);
}