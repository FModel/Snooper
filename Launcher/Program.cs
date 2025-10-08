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
using Snooper.Rendering.Components.Mesh;

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
const string mapping = "D:\\FModel\\.data\\++Fortnite+Release-37.30-CL-45814998-Windows_oo.usmap";
const string key = "0x7408A7C7EC17B4BA5963642421C55E652333CBA0786E779D24AC31D3C3D8124D";
var version = new VersionContainer(EGame.GAME_UE5_6);
#elif VL
const string dir = "D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\Valorant_11_04.usmap";
const string key = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
var version = new VersionContainer(EGame.GAME_Valorant);
#elif GTA
const string dir = "D:\\Games\\GTA Vice City - Definitive Edition\\Gameface\\Content\\Paks";
const string mapping = "";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_GTATheTrilogyDefinitiveEdition);
#elif BOB
const string dir = "D:\\Games\\SBSP - The Cosmic Shake\\CosmicShake\\Content\\Paks";
const string mapping = "";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE4_27);
#elif SUPRA
const string dir = "D:\\CSGO\\steamapps\\common\\Supraworld\\Supraworld\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\5.6.1-44394996+++UE5+Release-5.6-Supraworld.usmap";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE5_6);
#elif CAT
const string dir = "D:\\CSGO\\steamapps\\common\\Stray\\Hk_project\\Content\\Paks";
const string mapping = "";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_Stray);
#endif

var provider = new DefaultFileProvider(dir, SearchOption.AllDirectories, version);
if (!string.IsNullOrEmpty(mapping))
    provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mapping);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey(key));
provider.PostMount();
provider.LoadVirtualPaths();

var snooper = new SnooperWindow(144, 1500, 900, false);
var scene = new Actor("Scene");
// scene.Children.Add(new SkyboxActor());

var grid = new Actor("Grid");
grid.Components.Add(new GridComponent());
scene.Children.Add(grid);

var camera = new CameraActor("Camera");
camera.CameraComponent.LocalTransform.Position -= Vector3.UnitZ * 5;
camera.CameraComponent.LocalTransform.Position += Vector3.UnitY * 1.5f;
scene.Children.Add(camera);

switch (provider.ProjectName)
{
    case "ShooterGame":
    {
        // Ascent
        // Bonsai
        // Duality
        // FoxTrot
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
        
        var world = new WorldActor(provider.LoadPackageObject<UWorld>("Gameface/Content/ViceCity/Maps/VCWorld/VCWorld.VCWorld"), WorldActorType.LevelStreaming);
        
        scene.Children.Add(world);
        break;
    }
    case "CosmicShake":
    {
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("CosmicShake/Content/CS/Characters/SpongeBob/SK_SpongeBob_RoboSpongeBob.SK_SpongeBob_RoboSpongeBob"), new FTransform(new FVector(-100, 0, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("CosmicShake/Content/CS/Characters/Patrick/SK_Patrick_Default.SK_Patrick_Default"), new FTransform(new FVector(100, 0, 0))));
        // break;
        
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/Global/BB_P_Background.BB_P_Background"), WorldActorType.Landscape));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z11_HUB10/BB_Z11_HUB10_Geo.BB_Z11_HUB10_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z09_HUB9/BB_Z09_HUB9_Geo.BB_Z09_HUB9_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z08_HUB8/BB_Z08_HUB8_Geo.BB_Z08_HUB8_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z07_HUB7/BB_Z07_HUB7_Geo.BB_Z07_HUB7_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z06_HUB6/BB_Z06_HUB6_Geo.BB_Z06_HUB6_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z05_HUB5/BB_Z05_HUB5_Geo.BB_Z05_HUB5_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z04_HUB4/BB_Z04_HUB4_Geo.BB_Z04_HUB4_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z03_HUB3/BB_Z03_HUB3_Geo.BB_Z03_HUB3_Geo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/BB_Z02_HUB2/BB_Z02_HUB2_Geo.BB_Z02_HUB2_Geo")));
        break;
    }
    case "Supraworld":
    {
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Supraworld/Plugins/GameFeatures/Supraworld/Supraworld/Content/Maps/Supraworld.Supraworld")));
        break;
    }
    case "Hk_project":
    {
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Hk_project/Content/Map/_MainGame/06_MidTown/remaster/MIDTOWN_Club_GRAPH.MIDTOWN_Club_GRAPH")));
        break;
    }
    case "FortniteGame":
    {
        // var body = new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/F_MED_RoseForm.F_MED_RoseForm"));
        // var head = new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Heads/F_MED_RoseForm_Head/Meshes/F_MED_RoseForm_Head.F_MED_RoseForm_Head"));
        // var acc1 = new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/Parts/F_MED_RoseForm_FaceAcc.F_MED_RoseForm_FaceAcc"));
        //
        // head.Relation = body;
        // acc1.Relation = body;
        //
        // var character = new Actor(body.Name);
        // character.Components.Add(body); // root component
        // character.Components.Add(head);
        // character.Components.Add(acc1);
        //
        // scene.Children.Add(character);
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Gadgets/Assets/VinderTech_GliderChute/Glider_Rumble_Female/Meshes/Rumble_Female_Glider.Rumble_Female_Glider"), new FTransform(new FVector(200, 0, 100))));
        // break;
        
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/45a59717-4e0e-0359-cd14-b08bf44c08d9/Content/HammerFall_Level.HammerFall_Level")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/5e133425-4c5e-7cfb-1d0a-8db2bed53723/Content/StormChaser_Level.StormChaser_Level")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/a8a3061c-49c1-4f71-2604-ae9d3414b8d6/Content/Skyline_Level.Skyline_Level")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/99be4597-4530-1344-d0b9-4d8ab554db97/Content/Mandu_Shell.Mandu_Shell")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/45593d43-4a37-2dd7-f6bd-96a48fcd965a/Content/Cinderwatch_Shell.Cinderwatch_Shell")));
        
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain.Hermes_Terrain"), WorldActorType.Landscape));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/913GK60G7TI7QBDT2VE9MPL9L.Hermes_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain/_Generated_/4PA8JPWMPCH2G4AVRFN4YRF7A.Hermes_Terrain")));
        
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BlastBerryMap/Content/Maps/BlastBerry_Terrain.BlastBerry_Terrain")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/CloudberryMapContent/Content/Athena/Apollo/Maps/POI/Apollo_POI_Agency.Apollo_POI_Agency")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/DelMar/DelMarGame/Content/Environments/Desert/Levels/Level_DM_NeonCity_SmallBuilding_A.Level_DM_NeonCity_SmallBuilding_A")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/Figment/Figment_S05_Map/Content/Athena_Terrain_S05.Athena_Terrain_S05"), WorldActorType.Landscape));
        break;
    }
}

snooper.AddToScene(scene);
snooper.Run();