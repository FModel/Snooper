using System.Diagnostics;
using System.Numerics;
using CUE4Parse.Utils;

namespace Snooper;

public static class Settings
{
    public static readonly string APP_PATH = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
    public static readonly string APP_VERSION = FileVersionInfo.GetVersionInfo(APP_PATH).FileVersion;
    public static readonly string APP_COMMIT_ID = FileVersionInfo.GetVersionInfo(APP_PATH).ProductVersion?.SubstringAfter('+');
    public static readonly string APP_SHORT_COMMIT_ID = APP_COMMIT_ID[..7];
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

    public const int DefaultWidthHeight = 1;
    public const string NoName = "Unnamed";
    public const int MaxTextureMipSize = 1024;
    public const int MaxNumberOfLods = 8;
    public const int NumberOfSamples = 4;
    public const float GlobalScale = 0.01f;

    public const int TessellationQuadCount = 4; // change this to increase the resolution of the base landscape mesh (power of 2)
    public static float TessellationScaleFactor => 1.0f / TessellationQuadCount;
    public static int TessellationQuadCountTotal => TessellationQuadCount * TessellationQuadCount;
    public static int TessellationIndicesPerQuad => TessellationQuadCountTotal * 4;
}
