using System.Numerics;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
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

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}]: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Literate)
    .CreateLogger();

OodleHelper.Initialize();
ZlibHelper.Initialize(ZlibHelper.DLL_NAME);

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
var scene = new Actor("Scene");
scene.Children.Add(new SkyboxActor());

var grid = new Actor("Grid");
grid.Components.Add(new GridComponent());
scene.Children.Add(grid);

var camera = new CameraActor("Camera");
camera.Transform.Position -= Vector3.UnitZ * 5;
camera.Transform.Position += Vector3.UnitY * 1.5f;
scene.Children.Add(camera);

switch (provider.ProjectName)
{
    case "ShooterGame":
    {
        // Ascent
        // Bonsai
        // Duality
        // Foxtrot
        // Infinity
        // Jam
        // Juliett
        // Pitt
        // Port
        // Poveglia
        // PovegliaV2
        // Rook
        // Triad
        
        scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new FTransform(new FVector(0, 200, 0))));
        scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new FTransform(new FVector(0, -200, 0))));
        scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/Sphere.Sphere"), new FTransform(new FVector(200, 0, 100))));
        break;
        
        var files = provider.Files.Values.Where(x => x is { Directory: "ShooterGame/Content/Maps/Bonsai", Extension: "umap" });
        foreach (var file in files)
        {
            var parts = file.NameWithoutExtension.Split('_');
            if (parts.Length < 2 || parts[1] != "Art" || parts[^1] == "VFX") continue;
    
            scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>(file.PathWithoutExtension + "." + file.NameWithoutExtension)));
        }
        break;
    }
    case "FortniteGame":
    {
        // var glider = new MeshActor(
        //     provider.LoadPackageObject<USkeletalMesh>(
        //         "FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Gadgets/Assets/VinderTech_GliderChute/Glider_Rumble_Female/Meshes/Rumble_Female_Glider.Rumble_Female_Glider"),
        //     new FTransform(new FVector(-100, 0, 0)));
        //
        // glider.InstancedTransform.AddLocalInstance(new FTransform(new FVector(-300, 0, 0)));
        // scene.Children.Add(glider);
        //
        // var character = new MeshActor(provider.LoadPackageObject<USkeletalMesh>(
        //         "FortniteGame/Content/Characters/Player/Male/Large/Bodies/M_LRG_Rumble/Meshes/M_LRG_Rumble.M_LRG_Rumble"),
        //     new FTransform(new FVector(100, 0, 0)));
        //
        // character.InstancedTransform.AddLocalInstance(new FTransform(new FVector(300, 0, 0)));
        //
        // scene.Children.Add(character);
        
        // var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain.Hermes_Terrain"));
        // var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BlastBerryMap/Content/Maps/BlastBerry_Terrain.BlastBerry_Terrain"), null, true);
        var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/7I1F34J21MNNF9A96V9PGFNVE.Hermes_Terrain"));
        // var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/DelMar/Levels/PirateAdventure/Content/PA_3DLabTrackA.PA_3DLabTrackA"));
        // var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/DelMar/Levels/GoldRush/Content/DelMar_Racing_ProjectA.DelMar_Racing_ProjectA"));
        // var world = new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/CloudberryMapContent/Content/Athena/Apollo/Maps/POI/Apollo_POI_Agency.Apollo_POI_Agency"));

        scene.Children.Add(world);
        break;
    }
}

snooper.AddToScene(scene);
snooper.Run();