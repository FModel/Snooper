using System.Diagnostics;
using System.Numerics;
using CUE4Parse.Utils;

namespace Snooper;

public static class Settings
{
    // OpenGL is a right-handed coordinate system
    public static readonly Vector3 ForwardVector = -Vector3.UnitZ;
    public static readonly Vector3 UpVector = Vector3.UnitY;
    public static readonly Vector3 RightVector = Vector3.UnitX;

    public static readonly string APP_PATH = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
    public static readonly string APP_VERSION = FileVersionInfo.GetVersionInfo(APP_PATH).FileVersion;
    public static readonly string APP_COMMIT_ID = FileVersionInfo.GetVersionInfo(APP_PATH).ProductVersion?.SubstringAfter('+');
    public static readonly string APP_SHORT_COMMIT_ID = APP_COMMIT_ID[..7];
    public static readonly DateTime APP_BUILD_DATE = File.GetLastWriteTime(APP_PATH);

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
