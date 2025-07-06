using System.Numerics;
using Snooper.Core.Containers.Resources;

namespace Snooper.Rendering.Components.Mesh;

public struct PerInstanceLandscapeData : IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
    public long Heightmap { get; set; }
    public Vector2 ScaleBias { get; set; }
}