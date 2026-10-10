using System.Numerics;
using System.Reflection;
using CUE4Parse.Utils;

namespace Snooper;

public static class Settings
{
    private static readonly Assembly _assembly = typeof(Settings).Assembly;
    public static readonly string APP_PATH = string.IsNullOrEmpty(_assembly.Location) ? Environment.ProcessPath ?? string.Empty : _assembly.Location;
    public static readonly string APP_COMMIT_ID = _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.SubstringAfter('+') ?? string.Empty;
    public static readonly string APP_SHORT_COMMIT_ID = APP_COMMIT_ID.Length >= 7 ? APP_COMMIT_ID[..7] : APP_COMMIT_ID;
    public static readonly DateTime APP_BUILD_DATE = File.GetLastWriteTime(APP_PATH);

    // OpenGL is a right-handed coordinate system
    public static readonly Vector3 ForwardVector = -Vector3.UnitZ;
    public static readonly Vector3 UpVector = Vector3.UnitY;
    public static readonly Vector3 RightVector = Vector3.UnitX;

    // debug visualization colors
    public static readonly Vector3 VisibleMeshBounds = new(0.05f, 0.90f, 0.35f);
    public static readonly Vector3 HiddenMeshBounds = new(0.35f, 0.45f, 0.90f);
    public static readonly Vector3 LandscapeBounds = new(0.95f, 0.55f, 0.05f);
    public static readonly Vector3 PointLight = new(0.95f, 0.15f, 0.45f);
    public static readonly Vector3 SpotLight = new(0.05f, 0.80f, 0.75f);
    public static readonly Vector3 RectLight = new(0.45f, 0.20f, 0.95f);
    public static readonly Vector3 DirectionalLight = new(0.95f, 0.80f, 0.10f);

    public const uint AxisColorX = 0xFF_55_3E_E9;
    public const uint AxisColorY = 0xFF_28_CE_8C;
    public const uint AxisColorZ = 0xFF_F9_9B_31;
    public const uint AxisColorW = 0xFF_8A_8A_8A;

    public static readonly Vector4 RedColor = new(1f, 0.4f, 0.4f, 1f);
    public static readonly Vector4 OrangeColor = new(1f, 0.5f, 0f, 1f);
    public static readonly Vector4 YellowColor = new(1f, 1f, 0.4f, 1f);
    public static readonly Vector4 GreenColor = new(0.4f, 1f, 0.4f, 1f);

    public const string TrashIcon = "\uf1f8";
    public const string AddIcon = "\uf055";
    public const string ClipboardListIcon = "\uf46d";
    public const string EyeSlashIcon = "\uf070";
    public const string FocusIcon = "\uf05b";
    public const string JobIcon = "\uf085";
    public const string ImageIcon = "\uf03e";
    public const string ImagesIcon = "\uf302";
    public const string DatabaseIcon = "\uf1c0";
    public const string CopyIcon = "\uf0c5";
    public const string SpeedIcon = "\uf3fd";
    public const string FovIcon = "\uf065";
    public const string LoopIcon = "\uf021";
    public const string InfinityIcon = "\uf534";
    public const string BoxArchiveIcon = "\uf187";
    public const string TerminalIcon = "\uf120";
    public const string ChartGanttIcon = "\ue0e4";
    public const string BarsProgressIcon = "\uf828";
    public const string CubeIcon = "\uf1b2";
    public const string CubesIcon = "\uf1b3";
    public const string DiceD6Icon = "\uf6d1";
    public const string TriangleExclamationIcon = "\uf071";
    public const string LockIcon = "\uf023";
    public const string SpinnerIcon = "\uf110";
    public const string CityIcon = "\uf64f";
    public const string DownloadIcon = "\uf019";
    public const string EjectIcon = "\uf052";
    public const string RoadIcon = "\uf018";
    public const string BinocularsIcon = "\uf1e5";
    public const string GearIcon = "\uf013";
    public const string ChartPieIcon = "\uf200";
    public const string MagnifyingGlassIcon = "\uf002";
    public const string FolderOpenIcon = "\uf07c";
    public const string BanIcon = "\uf05e";
    public const string PlayIcon = "\uf04b";
    public const string StopIcon = "\uf04d";
    public const string FileImportIcon = "\uf56f";
    public const string FileExportIcon = "\uf56e";
    public const string PowerOffIcon = "\uf011";
    public const string PaletteIcon = "\uf53f";
    public const string EyeIcon = "\uf06e";
    public const string EyeLowVisionIcon = "\uf2a8";
    public const string CameraIcon = "\uf030";
    public const string BookIcon = "\uf02d";
    public const string KeyboardIcon = "\uf11c";
    public const string CircleInfoIcon = "\uf05a";
    public const string EarthEuropeIcon = "\uf7a2";
    public const string ArrowRotateLeftIcon = "\uf0e2";
    public const string AngleLeftIcon = "\uf104";
    public const string AngleRightIcon = "\uf105";
    public const string RightLeftIcon = "\uf362";
    public const string DrawPolygonIcon = "\uf5ee";
    public const string MapPinIcon = "\uf276";
    public const string TurnUpIcon = "\uf3bf";
    public const string LinkIcon = "\uf0c1";
    public const string LinkSlashIcon = "\uf127";
    public const string ArrowsUpDownLeftRightIcon = "\uf047";
    public const string RotateIcon = "\uf2f1";
    public const string RulerCombinedIcon = "\uf546";
    public const string LightbulbIcon = "\uf0eb";
    public const string CircleHalfStrokeIcon = "\uf042";
    public const string ChalkboardIcon = "\uf51b";
    public const string FontIcon = "\uf031";
    public const string BugIcon = "\uf188";
    public const string ChartLineIcon = "\uf201";
    public const string MicrochipIcon = "\uf2db";
    public const string ToggleOnIcon = "\uf205";
    public const string ToggleOffIcon = "\uf204";
    public const string PlugIcon = "\uf1e6";
    public const string BoneIcon = "\uf5d7";
    public const string UpRightAndDownLeftFromCenterIcon = "\uf424";
    public const string DownLeftAndUpRightToCenterIcon = "\uf422";
    public const string PlusIcon = "\uf067";
    public const string CloudIcon = "\uf0c2";
    public const string SmogIcon = "\uf75f";
    public const string CloudSunIcon = "\uf6c4";
    public const string SunIcon = "\uf185";

    public const string ViewportWindow = $"{CubeIcon}  Viewport";
    public const string SceneHierarchyWindow = $"{RoadIcon}  Hierarchy";
    public const string InspectorWindow = $"{BinocularsIcon}  Inspector";
    public const string TimelineWindow = $"{ChartGanttIcon}  Timeline";
    public const string LogWindow = $"{TerminalIcon}  Logs";
    public const string SettingsWindow = $"{GearIcon}  Settings";
    public const string ContentWindow = $"{BoxArchiveIcon}  Content";
    public const string MorphTargetsWindow = $"{BarsProgressIcon}  Morph Targets";
    public const string SkeletonWindow = $"{BoneIcon}  Skeleton Tree";
    public const string MaterialInspectorWindow = $"{PaletteIcon}  Material Inspector";
    public const string TextureInspectorWindow = $"{ImageIcon}  Texture Inspector";
    public const string MemoryWindow = $"{DatabaseIcon}  Memory";

    public const int DefaultWidthHeight = 1;
    public const string NoName = "Unnamed";
    public const int MaxNumberOfLods = 8;
    public const int MaxInstancesPerDraw = 1024;
    public const int MaxInstancesPerCullThread = 8;
    public const int NumberOfSamples = 4;
    public const float GlobalScale = 0.01f;

    public const int MaxShadowCascades = 4;
    public const int MaxLocalShadowCasters = 4;
    public const int ShadowResolution = 2048;
    public const int MaxShadowViews = MaxShadowCascades + MaxLocalShadowCasters < 32 ? MaxShadowCascades + MaxLocalShadowCasters : 32;
    public const int MaxCullingViews = 1 + MaxShadowViews;

    public const int MaxWeightmaps = 4;
    public const int TessellationQuadCount = 4; // change this to increase the resolution of the base landscape mesh (power of 2)
    public const int TessellationQuadCountTotal = TessellationQuadCount * TessellationQuadCount;
    public const int TessellationIndicesPerQuad = TessellationQuadCountTotal * 4;
}
