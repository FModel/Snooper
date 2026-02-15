using System.Numerics;
using CUE4Parse_Conversion.Textures.BC;
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
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}]: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Literate)
    .WriteTo.Sink(new ImGuiSink())
    .CreateLogger();

OodleHelper.Initialize();
ZlibHelper.Initialize(ZlibHelper.DLL_NAME);
DetexHelper.Initialize("D:\\FModel\\.data\\Detex.dll");

#if FN
const string dir = "D:\\Games\\Fortnite\\FortniteGame\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\++Fortnite+Release-39.00-CL-48444883_br.usmap";
const string key = "0x8E95784F8ECC94113349AE1678C62EBB50ABBA8C10422E7C5D8399B13DA07AE8";
var version = new VersionContainer(EGame.GAME_UE5_8);
#elif VL
const string dir = "D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\VALORANT_12.01_zs.usmap";
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
#elif COE33
const string dir = "D:\\Games\\Clair Obscur - Expedition 33\\Sandfall\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\5.4.4-61339+++streams+ProjectW-release-Sandfall.usmap";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE5_4);
#elif CARS
const string dir = "D:\\Games\\Cars_Overdrive\\Cars_Overdrive\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\5.4.4-35576357+++UE5+Release-5.4-Cars_Overdrive.usmap";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE5_4);
#elif R9W
const string dir = "D:\\CSGO\\steamapps\\common\\Race of the Nine Worlds Demo\\WindowsNoEditor\\r9w\\Content\\Paks";
const string mapping = "";
const string key = "0x34FC366196D4535B12D4B0A67072B5F973CDA66D5BBAD30D26C39503544A6948";
var version = new VersionContainer(EGame.GAME_UE4_27);
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

var camera = new CameraActor("Camera");
camera.CameraComponent.LocalTransform.Position -= Vector3.UnitZ * 5;
camera.CameraComponent.LocalTransform.Position += Vector3.UnitY * 1.5f;
scene.Children.Add(camera);

var grid = new Actor("Grid");
grid.Components.Add(new GridComponent());
scene.Children.Add(grid);

var sun = new Actor("Sun Light");
sun.Components.Add(new DirectionalLightComponent(MathF.PI, new Vector3(1.0f, 0.87f, 0.72f), new Transform(new Quaternion(new Vector3(0.5f, -0.5f, 0.0f), 1.0f)), "Directional Light"));
scene.Children.Add(sun);

scene.Children.Add(new SkyboxActor());

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

        // camera.CameraComponent.FarClipPlane = 100f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new FTransform(new FVector(0, 200, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new FTransform(new FVector(0, -200, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/sphere.Sphere"), new FTransform(new FVector(200, 0, 100))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/Asset/Props/Foliage/9/Foliage_9_IvyTopA.Foliage_9_IvyTopA"), new FTransform(new FVector(200, 100, 100))));
        // break;

        var files = provider.Files.Values.Where(x => x is { Directory: "ShooterGame/Content/Maps/Bonsai", Extension: "umap" });
        foreach (var file in files)
        {
            var parts = file.NameWithoutExtension.Split('_');
            if (parts.Length < 2) continue;

            var trigger = parts[1];
            if (trigger is "Art" or "Skybox" or "Audio" or "Lighting" or "Mode" or "TeamSpawnPoints")
            {
                var obj = file.NameWithoutExtension;
                if (obj == "Duality_Art_MIdPathB")
                    obj = "Duality_Art_MidPathB";

                scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>(file.PathWithoutExtension + "." + obj)));
            }
        }
        break;
    }
    case "Gameface":
    {
        // camera.CameraComponent.FarPlaneDistance = 1000f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("Gameface/Content/ViceCity/Characters/Peds/SK_hmotr.SK_hmotr")));
        // break;

        var world = new WorldActor(provider.LoadPackageObject<UWorld>("Gameface/Content/ViceCity/Maps/VCWorld/VCWorld.VCWorld"), WorldActorType.LevelStreaming);

        scene.Children.Add(world);
        break;
    }
    case "Cars_Overdrive":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Cars_Overdrive/Content/LV_Demo.LV_Demo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Cars_Overdrive/Content/LV_Final_World.LV_Final_World")));
        break;
    }
    case "r9w":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Menu/00_MainMenu.00_MainMenu")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Rinky_World/Map_list/01_Prologue_sprint_pursuit.01_Prologue_sprint_pursuit")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Rinky_World/Map_list/Race_list/Circle_Rinky_Planet_01.Circle_Rinky_Planet_01")));
        break;
    }
    case "CosmicShake":
    {
        // camera.CameraComponent.FarPlaneDistance = 1000f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("CosmicShake/Content/CS/Characters/SpongeBob/SK_SpongeBob_RoboSpongeBob.SK_SpongeBob_RoboSpongeBob"), new FTransform(new FVector(-100, 0, 0))));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<USkeletalMesh>("CosmicShake/Content/CS/Characters/Patrick/SK_Patrick_Default.SK_Patrick_Default"), new FTransform(new FVector(100, 0, 0))));
        // break;

        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("CosmicShake/Content/CS/Maps/BikiniBottom/Global/BB_P_Background.BB_P_Background")));
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
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Hk_project/Content/Map/_MainGame/01_InsideTheWall/InsideTheWall_GRAPH.InsideTheWall_GRAPH")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Hk_project/Content/Map/_MainGame/01_InsideTheWall/ToDeadCity_TRANS/ToDeadCity_GRAPH.ToDeadCity_GRAPH")));
        break;
    }
    case "Sandfall":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/CleasTower/CleasTower_GroundFloorEntrance.CleasTower_GroundFloorEntrance")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/WorldMap/Level_WorldMap_Main_V2.Level_WorldMap_Main_V2")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/Lumiere/Level_Lumiere_Main_V2.Level_Lumiere_Main_V2")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/RedForest/Level_RedForest_Main.Level_RedForest_Main")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/CleasTower/Level_Side_CleasTower.Level_Side_CleasTower")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/Goblu/Level_Goblu_Main_V5.Level_Goblu_Main_V5")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/WorldMap/Camps/Level_Camp_Main.Level_Camp_Main"))); // hits hard on ram and skeletal meshes slow to load
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Sandfall/Content/Levels/WorldMap/GestralBeaches/Level_GestralBeach_Day.Level_GestralBeach_Day")));
        break;
    }
    case "FortniteGame":
    {
        // camera.CameraComponent.FarPlaneDistance = 1000f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // var character = new MeshActor(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/F_MED_RoseForm.F_MED_RoseForm"));
        // character.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Heads/F_MED_RoseForm_Head/Meshes/F_MED_RoseForm_Head.F_MED_RoseForm_Head")));
        // character.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/Parts/F_MED_RoseForm_FaceAcc.F_MED_RoseForm_FaceAcc")));
        // character.Components.Add(new TextRenderComponent("Character (Clip)", 16, transform: new Transform(new Vector3(0, 1.8f, 0), new Quaternion(1, 0, 0, 1))));
        // scene.Children.Add(character);
        //
        // var glider = new MeshActor(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Gadgets/Assets/VinderTech_GliderChute/Glider_Rumble_Female/Meshes/Rumble_Female_Glider.Rumble_Female_Glider"), new FTransform(new FVector(200, 0, 100)));
        // glider.Components.Add(new TextRenderComponent("Glider (Kayari Buta)", 16, transform: new Transform(new Vector3(0, 2.2f, 0), new Quaternion(1, 0, 0, 1))));
        // scene.Children.Add(glider);
        //
        // var actor = new Actor("Origin Indicator");
        // actor.Components.Add(new TextRenderComponent("Origin", 54, new Vector3(1.0f, 0, 0), transform: new Transform(new Vector3(0, 0.001f, 0))));
        // scene.Children.Add(actor);
        //
        // var overhang = new MeshActor(provider.LoadPackageObject<UStaticMesh>("FortniteGame/Content/Environments/Asteria/Sets/Dojo/Meshes/SM_Asteria_Dojo_Overhang_Outer_Crn_A_A.SM_Asteria_Dojo_Overhang_Outer_Crn_A_A"), new Transform(new Vector3(0, 4, -3)));
        // overhang.Components.Add(new TextRenderComponent("Asteria Dojo Overhang", 32, transform: new Transform(new Vector3(0, -1.5f, 0), new Quaternion(1, 0, 0, 1))));
        // scene.Children.Add(overhang);
        // break;

        grid.Parent?.Children.Remove(grid);
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/BRMapCh6/Content/Maps/Hermes_Terrain.Hermes_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/WildEstate/Content/Maps/WildEstate_Terrain.WildEstate_Terrain")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/Hera_Map/Content/Maps/Hera_Terrain.Hera_Terrain")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/CloudberryMapContent/Content/Athena/Apollo/Maps/POI/Apollo_POI_Agency.Apollo_POI_Agency")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/DelMar/DelMarGame/Content/Environments/Desert/Levels/Level_DM_NeonCity_SmallBuilding_A.Level_DM_NeonCity_SmallBuilding_A")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/1x1/Artemis_1x1_BusStation_a.Artemis_1x1_BusStation_a")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/3x3/Artemis_3x3_Generic_House_a.Artemis_3x3_Generic_House_a")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/3x3/Artemis_3x3_Generic_House_c.Artemis_3x3_Generic_House_c")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/3x3/Artemis_3x3_IOBorderTower_PTY_02.Artemis_3x3_IOBorderTower_PTY_02")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/5x5/Artemis_SUB_5x5_House_m3.Artemis_SUB_5x5_House_m3")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/5x5/Artemis_Sub_5x5_Retail_a_opt.Artemis_Sub_5x5_Retail_a_opt")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/5x9/Artemis_5x9_SUB_CoastMotel_01_AB.Artemis_5x9_SUB_CoastMotel_01_AB")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Content/Athena/Artemis/Maps/Buildings/5x9/Artemis_SUB_5x9_IceCream_a.Artemis_SUB_5x9_IceCream_a")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FortniteGame/Plugins/GameFeatures/Figment/Figment_S06_Map/Content/Athena_Terrain_S06.Athena_Terrain_S06")));
        break;
    }
}

snooper.AddToScene(scene);
snooper.Run();
