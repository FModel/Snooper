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
const string mapping = "D:\\FModel\\.data\\++Fortnite+Release-37.00-CL-44501951-Windows_oo.usmap";
const string key = "0x20E23FDB8EF3D9503F6012072BAF4090EA6363A5E4BFBB457608C731914D8E83";
var version = new VersionContainer(EGame.GAME_UE5_6);
#elif VL
const string dir = "D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\Valorant_11_2.usmap";
const string key = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
var version = new VersionContainer(EGame.GAME_Valorant);
#elif GTA
const string dir = "D:\\Games\\GTA Vice City - Definitive Edition\\Gameface\\Content\\Paks";
const string mapping = "";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_GTATheTrilogyDefinitiveEdition);
#endif

var provider = new DefaultFileProvider(dir, SearchOption.TopDirectoryOnly, version);
if (!string.IsNullOrEmpty(mapping))
    provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mapping);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey(key));
provider.PostMount();
provider.LoadVirtualPaths();

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
        
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new FTransform(new FVector(0, 200, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new FTransform(new FVector(0, -200, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/Sphere.Sphere"), new FTransform(new FVector(200, 0, 100))));
        // break;
        
        var files = provider.Files.Values.Where(x => x is { Directory: "ShooterGame/Content/Maps/Bonsai", Extension: "umap" });
        foreach (var file in files)
        {
            var parts = file.NameWithoutExtension.Split('_');
            if (parts.Length < 2 || parts[1] != "Art" || parts[^1] == "VFX") continue;

            var obj = file.NameWithoutExtension;
            if (obj == "Duality_Art_MIdPathB")
                obj = "Duality_Art_MidPathB";
            
            scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>(file.PathWithoutExtension + "." + obj)));
        }
        break;
    }
    case "Gameface":
    {
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("Gameface/Content/ViceCity/Characters/Peds/SK_hmotr.SK_hmotr")));
        // break;
        
        var world = new WorldActor(provider.LoadPackageObject<UWorld>("Gameface/Content/ViceCity/Maps/VCWorld/VCWorld.VCWorld"));
        
        scene.Children.Add(world);
        break;
    }
    case "FortniteGame":
    {
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Content/Characters/Player/Male/Large/Bodies/M_LRG_Rumble/Meshes/M_LRG_Rumble.M_LRG_Rumble")));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/Sphere.Sphere"), new FTransform(new FVector(200, 0, 100))));
        // break;
        
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain.Hermes_Terrain"), null, WorldActorType.Landscape));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/79Q7M8Z52AB6N5DRWWAGJ3MPT.Hermes_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/C5M0Z1058UH49VHUBP8Z4JJIQ.Hermes_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/9IQ76DL0BWAPJMACBUTRETQJW.Hermes_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/BXT3A7SBMGWGLI1JOEFC1QDCW.Hermes_Terrain")));
        
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BlastBerryMap/Content/Maps/BlastBerry_Terrain.BlastBerry_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/CloudberryMapContent/Content/Athena/Apollo/Maps/POI/Apollo_POI_Agency.Apollo_POI_Agency")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/Figment/Figment_S05_Map/Content/Athena_Terrain_S05.Athena_Terrain_S05")));
        break;
    }
}

snooper.AddToScene(scene);
snooper.Run();