using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using OpenTK.Windowing.Common;
using Snooper;

var version = new VersionContainer(EGame.GAME_Valorant, ETexturePlatform.DesktopMobile);
var provider = new DefaultFileProvider("D:\\Games\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks", SearchOption.TopDirectoryOnly, version);
provider.Initialize();
provider.SubmitKey(new FGuid(), new FAesKey("0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6"));
provider.PostMount();

var snooper = new SnooperWindow(144, 1500, 900, new Version(4, 6), false);
snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Characters/Clay/S0/3P/Models/TP_Clay_S0_Skelmesh.TP_Clay_S0_Skelmesh"));
snooper.AddToScene(provider.LoadPackageObject("ShooterGame/Content/Environment/HURM_Helix/Asset/Props/Boat/0/Boat_0_LongThaiB.Boat_0_LongThaiB"));

snooper.Run();