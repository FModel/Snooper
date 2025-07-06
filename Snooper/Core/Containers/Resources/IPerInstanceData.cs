using System.Numerics;

namespace Snooper.Core.Containers.Resources;

public interface IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
}

public struct PerInstanceData : IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
}