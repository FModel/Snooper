using System.Numerics;
using System.Runtime.InteropServices;

namespace Snooper.Rendering.Containers;

[StructLayout(LayoutKind.Sequential)]
public struct ClusterAABB
{
    public Vector3 MinPoint;
    public float Padding1;
    public Vector3 MaxPoint;
    public float Padding2;
}

[StructLayout(LayoutKind.Sequential)]
public struct ClusterData
{
    public uint Offset;   // Offset into light index list
    public uint Count;    // Number of lights in this cluster
}

public class ClusteringConstants
{
    public const int TileSize = 32;
    public const int MaxLightsPerCluster = 256;
    public const int MaxLights = 10000;
    public const int ZSlices = 16;
}
