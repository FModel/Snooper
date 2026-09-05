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
    public const string EyeSlashIcon = "\uf070";
    public const string FocusIcon = "\uf05b";
    public const string JobIcon = "\uf085";
    public const string TextureIcon = "\uf03e";
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
    public const string RoadIcon = "\uf018";
    public const string BinocularsIcon = "\uf1e5";
    public const string GearIcon = "\uf013";
    public const string ChartPieIcon = "\uf200";
    public const string MagnifyingGlassIcon = "\uf002";
    public const string FolderOpenIcon = "\uf07c";
    public const string BanIcon = "\uf05e";
    public const string PlayIcon = "\uf04b";
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

    public const string ViewportWindow = $"{CubeIcon}  Viewport";
    public const string SceneHierarchyWindow = $"{RoadIcon}  Hierarchy";
    public const string InspectorWindow = $"{BinocularsIcon}  Inspector";
    public const string TimelineWindow = $"{ChartGanttIcon}  Timeline";
    public const string LogWindow = $"{TerminalIcon}  Logs";
    public const string WorldSettingsWindow = $"{GearIcon}  Render World Settings";
    public const string SystemsWindow = $"{ChartPieIcon}  Systems";
    public const string ContentWindow = $"{BoxArchiveIcon}  Content";
    public const string MorphEditorWindow = $"{BarsProgressIcon}  Morph Editor";
    public const string MaterialEditorWindow = $"{PaletteIcon}  Material Editor";
    public const string TextureInspectorWindow = $"{TextureIcon}  Texture Inspector";

    public const int DefaultWidthHeight = 1;
    public const string NoName = "Unnamed";
    public const int MaxNumberOfLods = 8;
    public const int NumberOfSamples = 4;
    public const float GlobalScale = 0.01f;

    public const int MaxShadowCascades = 4;
    public const int MaxLocalShadowCasters = 4;
    public const int ShadowResolution = 2048;
    public const int MaxShadowViews = MaxShadowCascades + MaxLocalShadowCasters;
    public const int MaxCullingViews = 1 + MaxShadowViews;

    public const int TessellationQuadCount = 4; // change this to increase the resolution of the base landscape mesh (power of 2)
    public const int TessellationQuadCountTotal = TessellationQuadCount * TessellationQuadCount;
    public const int TessellationIndicesPerQuad = TessellationQuadCountTotal * 4;
}
