using System.Numerics;

namespace Snooper.Core.Containers.Resources;

/// <summary>
/// read back: gl_BaseInstance + gl_InstanceID
/// </summary>
public interface IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
}

public struct PerInstanceData : IPerInstanceData
{
    public Matrix4x4 Matrix { get; set; }
}