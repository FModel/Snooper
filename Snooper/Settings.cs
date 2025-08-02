namespace Snooper;

public static class Settings
{
    public const int MaxNumberOfLods = 8;
    public const int NumberOfSamples = 4;
    public const float GlobalScale = 0.01f;

    public const int TessellationQuadCount = 4; // change this to increase the resolution of the base landscape mesh (power of 2)
    public static float TessellationScaleFactor => 1.0f / TessellationQuadCount;
    public static int TessellationQuadCountTotal => TessellationQuadCount * TessellationQuadCount;
    public static int TessellationIndicesPerQuad => TessellationQuadCountTotal * 4;
}