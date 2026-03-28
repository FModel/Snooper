using System.Numerics;
using CUE4Parse_Conversion.Textures.BC;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using Editor;
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
const string mapping = "D:\\FModel\\.data\\++Fortnite+Release-40.00-CL-51746096_br.usmap";
const string key = "0x55AFB842F6B302A2DDC69A3C437DDEABBB3370C700F8814A70520BBABBF7B770";
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
#elif MR
const string dir = "D:\\CSGO\\steamapps\\common\\MarvelRivals\\MarvelGame\\Marvel\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\5.3.2-1967306+++depot_marvel+S2_2_release-Marvel+PY.usmap";
const string key = "0x0C263D8C22DCB085894899C3A3796383E9BF9DE0CBFB08C9BF2DEF2E84F29D74";
var version = new VersionContainer(EGame.GAME_MarvelRivals);
#elif WEST
const string dir = "D:\\CSGO\\steamapps\\common\\Far Far West Demo\\FarFarWest\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\507_FarFarWest.usmap";
const string key = "0xDD5543BCC9C387A03DEADD72473D6A2EAF491AF88FC47365EE963F8AFE16B2D9";
var version = new VersionContainer(EGame.GAME_UE5_7);
#elif BUS
const string dir = "D:\\CSGO\\steamapps\\common\\Denshattack! Demo\\Denshattack_Proto\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\5.6.1-0+UE5-Denshattack_Proto.usmap";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE5_6);
#elif UPS
const string dir = "D:\\CSGO\\steamapps\\common\\DEADLINE DELIVERY Demo\\DEADLINE_DELIVERY\\Content\\Paks";
const string mapping = "";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE4_27);
#elif EXA
const string dir = "D:\\Games\\UE Projects\\ContentExamples\\Saved\\StagedBuilds\\Windows\\ContentExamples\\Content\\Paks";
const string mapping = "D:\\FModel\\.data\\507_ContentExamples.usmap";
const string key = "0x0000000000000000000000000000000000000000000000000000000000000000";
var version = new VersionContainer(EGame.GAME_UE5_7);
#endif

var provider = new DefaultFileProvider(dir, SearchOption.AllDirectories, version, StringComparer.OrdinalIgnoreCase);
if (!string.IsNullOrEmpty(mapping))
    provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mapping);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey(key));
provider.PostMount();
provider.LoadVirtualPaths();

var snooper = new EditorWindow(144, 1500, 900, false);
var scene = new Actor("Scene");
scene.Components.Add(new BoxComponent(Vector3.Zero, Vector3.One));

var camera = new CameraActor("Camera");
camera.CameraComponent.LocalTransform.Position = new Vector3(1, 2, -0.5f);
camera.CameraComponent.LocalTransform.Rotation = new Quaternion(0, -1, 0, 1);
scene.Children.Add(camera);

var grid = new Actor("Grid");
grid.Components.Add(new GridComponent());
scene.Children.Add(grid);

var sun = new Actor("Sun Light");
sun.Components.Add(new DirectionalLightComponent(MathF.PI, new Vector3(1.0f, 0.87f, 0.72f), new Transform(new Quaternion(new Vector3(0.5f, -0.5f, 0.0f), 1.0f)), "Directional Light"));
scene.Children.Add(sun);

switch (provider.ProjectName)
{
    case "ShooterGame":
    {
        // camera.CameraComponent.FarClipPlane = 100f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // var textOrientation = new Quaternion(0.5f, 0.5f, -0.5f, 0.5f);
        //
        // var character1P = new Actor("Raze 1P");
        // character1P.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Clay/S0/1P/Models/FP_Clay_S0_Skelmesh.FP_Clay_S0_Skelmesh")));
        // character1P.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Equippables/Guns/Rifles/AK/Afterglow/Models/GN_AK_Afterglow_Lv2_Skelmesh.GN_AK_Afterglow_Lv2_Skelmesh"))
        // {
        //     AttachSocketName = "L_WeaponPoint"
        // });
        // character1P.Components.Add(new TextRenderComponent(character1P.Name, transform: new Transform(new Vector3(0, 1.9f, 0), textOrientation)));
        // scene.Children.Add(character1P);
        //
        // var character3P = new Actor("Raze 3P");
        // character3P.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"), new Vector3(0, 0, -1)));
        // character3P.Components.Add(new TextRenderComponent(character3P.Name, transform: new Transform(new Vector3(0, 2.2f, 0), textOrientation)));
        // scene.Children.Add(character3P);
        //
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"), new Vector3(-3, 0, 0)));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("Engine/Content/BasicShapes/sphere.Sphere"), new Vector3(-2, 4, 1)));
        // scene.Children.Add(new MeshActor(provider.LoadPackageObject<UStaticMesh>("ShooterGame/Content/Environment/Asset/Props/Foliage/9/Foliage_9_IvyTopA.Foliage_9_IvyTopA"), new Vector3(2, 1, 1)));
        // break;

        /*
         * Wraith
         * Pandemic
         * Vampire
         * Killjoy
         * Sarge (2)
         * Phoenix
         * _Core           NoSelection
         * _Core           Waiting
         * _Core           SequencerBase
         * _Core           Base
         * Breach
         * Gumshoe
         * Hunter
         * Thorne
         * Clay
         * Wushu
         * Grenadier
         * Mage            Base_Mage
         * Sprinter
         * Deadeye (1)
         * AggroBot        Aggrobot
         * BountyHunter
         * Cable
         * Rift
         * Sequoia
         * Guide
         * Stealth
        */

        const string Character = "Sarge";
        var actor = new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>($"ShooterGame/Content/Characters/{Character}/FXC_CharacterSelect_{Character}.FXC_CharacterSelect_{Character}_C"));
        // var cameraRig = actor.Components.OfType<SpatialComponent>().FirstOrDefault(x => x.Name == "CameraRig");
        // if (cameraRig != null && camera.RootComponent is { } spatial)
        // {
        //     spatial.SetLocalTransform(Transform.Identity);
        //     spatial.Relation = cameraRig;
        //     spatial.AttachSocketName = "Camera";
        // }
        scene.Children.Add(actor);

        // var actor = new Actor("Skeleton");
        // var anim = provider.LoadPackageObject<UAnimMontage>("ShooterGame/Content/Characters/Sarge/S0/CharSelect/3P/Anims/CS_Sarge_S0_CharSelect_Anim_Montage.CS_Sarge_S0_CharSelect_Anim_Montage");
        // var skeleton = new SkeletalMeshComponent(anim.Skeleton.Load<USkeleton>());
        // skeleton.SetAnimation(anim);
        // actor.Components.Add(skeleton);
        // scene.Children.Add(actor);

        // var actor = new Actor("Actor");
        // var mesh = new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("ShooterGame/Content/Characters/Deadeye/S0/3P/Models/TP_Deadeye_S0_Skelmesh.TP_Deadeye_S0_Skelmesh"));
        // mesh.SetAnimation(provider.LoadPackageObject<UAnimationAsset>("ShooterGame/Content/Characters/Deadeye/S0/Ability_4/3P/Anims/TP_Deadeye_S0_4_Card_Equip_Montage.TP_Deadeye_S0_4_Card_Equip_Montage"));
        // actor.Components.Add(mesh);
        // scene.Children.Add(actor);
        break;

        // Ascent
        // Bonsai
        // Duality
        // FoxTrot
        // Infinity
        // Jam
        // Juliett
        // Pitt
        // Port
        // Rook
        // Triad
        const string Map = "Bonsai";
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>($"ShooterGame/Content/Maps/{Map}/{Map}.{Map}")));
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

        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Gameface/Content/ViceCity/Maps/VCWorld/VCWorld.VCWorld")));
        break;
    }
    case "Cars_Overdrive":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Cars_Overdrive/Content/LV_Demo.LV_Demo")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Cars_Overdrive/Content/LV_Final_World.LV_Final_World")));
        break;
    }
    case "ContentExamples":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/ExampleProjectWelcome.ExampleProjectWelcome")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Level/Level_Landscape.Level_Landscape")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Level/Level_WorldPartitionStreaming.Level_WorldPartitionStreaming")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Level/Level_Streaming.Level_Streaming")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Level/Level_Volumes.Level_Volumes")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Geometry/StaticMeshes.StaticMeshes")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Lighting/Lighting_Realtime.Lighting_Realtime")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/AI/AI_NavMesh.AI_NavMesh")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("ContentExamples/Content/Maps/Animation/Animation_ControlRig.Animation_ControlRig")));
        break;
    }
    case "Marvel":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/TokyoCQ01/TokyoCQ01.TokyoCQ01")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/TokyoE01/TokyoE01_Art.TokyoE01_Art")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/NewYorkE01/NewYorkE01_Art.NewYorkE01_Art")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/AsgardE01/AsgardE01_Art.AsgardE01_Art")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/Arakko/ArakkoE01_Art.ArakkoE01_Art")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/WakandaMC01/WakandaC03/WakandaC03_Art.WakandaC03_Art")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Marvel/Content/Marvel/Maps/Lobby/Lobby_SevenDayActiveEvent_Bg1.Lobby_SevenDayActiveEvent_Bg1")));
        break;
    }
    case "r9w":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Menu/00_MainMenu.00_MainMenu")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Rinky_World/Map_list/01_Prologue_sprint_pursuit.01_Prologue_sprint_pursuit")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("r9w/Content/Maps/Rinky_World/Map_list/Race_list/Circle_Rinky_Planet_01.Circle_Rinky_Planet_01")));
        break;
    }
    case "FarFarWest":
    {
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("FarFarWest/Content/Characters/BP_NPC_RailwayWorker.BP_NPC_RailwayWorker_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("FarFarWest/Content/Quests/Assets/BP_GatlingAntenna.BP_GatlingAntenna_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("FarFarWest/Content/GPE/Assets/Train/BP_TrainExtraction.BP_TrainExtraction_C")));

        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FarFarWest/Content/Levels/L_Map_Area41.L_Map_Area41"))); // TODO: not a big map but lags a lot, maybe something to do with the number of instances
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("FarFarWest/Content/Levels/L_Map_Tuto.L_Map_Tuto")));
        break;
    }
    case "Denshattack_Proto":
    {
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Denshattack_Proto/Content/Levels/MainLevel.MainLevel")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Denshattack_Proto/Content/Denshattack/Levels/WorldMap.WorldMap")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Denshattack_Proto/Content/Denshattack/Levels/3-Shikoku/3-2-2_KochiTrickpark_Art.3-2-2_KochiTrickpark_Art")));
        scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("Denshattack_Proto/Content/Denshattack/Levels/3-Shikoku/3-2-2_KochiTrickpark_Design.3-2-2_KochiTrickpark_Design")));
        break;
    }
    case "DEADLINE_DELIVERY":
    {
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Monkeys/monke_NPC.monke_NPC_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Monkeys/monke_NPC_Car.monke_NPC_Car_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Cars/Car_MASTER.Car_MASTER_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Cars/CarMP_MASTER.CarMP_MASTER_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Monkeys/Inspector/monke_Inspector.monke_Inspector_C")));
        scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/NPCs/Bus.Bus_C")));
        // scene.Children.Add(new BlueprintActor(provider.LoadPackageObject<UBlueprintGeneratedClass>("DEADLINE_DELIVERY/Content/Blueprints/Cars/Hippievan/Car_HIPPIEVAN.Car_HIPPIEVAN_C")));

        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("DEADLINE_DELIVERY/Content/Maps/Map18.Map18")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("DEADLINE_DELIVERY/Content/Maps/Map1.Map1")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("DEADLINE_DELIVERY/Content/Maps/Map17.Map17")));
        // scene.Children.Add(new WorldActor(provider.LoadPackageObject<UWorld>("DEADLINE_DELIVERY/Content/Maps/Map2.Map2")));
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
        // camera.CameraComponent.FarClipPlane = 1000f;
        // grid.Components.Clear();
        // grid.Components.Add(new OpaqueGridComponent());
        //
        // var character = new MeshActor(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/F_MED_RoseForm.F_MED_RoseForm"));
        // character.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Heads/F_MED_RoseForm_Head/Meshes/F_MED_RoseForm_Head.F_MED_RoseForm_Head"))
        // {
        //     AttachSocketName = "root"
        // });
        // character.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Characters/Player/Female/Medium/Bodies/F_MED_RoseForm/Meshes/Parts/F_MED_RoseForm_FaceAcc.F_MED_RoseForm_FaceAcc"))
        // {
        //     AttachSocketName = "root"
        // });
        // character.Components.Add(new SkeletalMeshComponent(provider.LoadPackageObject<USkeletalMesh>("FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Accessories/FORT_Backpacks/Backpack_M_MED_BadCat/Meshes/M_MED_BadCat_Pack.M_MED_BadCat_Pack"))
        // {
        //     AttachSocketName = "Backpack_BR"
        // });
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

snooper.Manager.RootActor = scene;
snooper.Run();
