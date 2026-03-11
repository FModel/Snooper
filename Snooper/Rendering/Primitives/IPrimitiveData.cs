using System.Numerics;

namespace Snooper.Rendering.Primitives;

public interface TPrimitiveData<T> where T : unmanaged
{
    public T[]? Vertices { get; }
    public uint[]? Indices { get; }

    // ---------- optional data ----------

    public int[]? Colors { get; }

    /// <summary>
    /// flat array of all bone influences across all vertices, stored contiguously, and free of zeros
    /// </summary>
    public uint[]? BoneInfluences { get; }

    /// <summary>
    /// the number of influences that vertex contributes to <see cref="BoneInfluences"/>.
    /// </summary>
    public byte[]? BoneInfluenceCounts { get; }
}

public abstract class PrimitiveData<T> : TPrimitiveData<T> where T : unmanaged
{
    public T[]? Vertices { get; protected init; }
    public uint[]? Indices { get; protected init; }

    public int[]? Colors { get; protected init; }
    public uint[]? BoneInfluences { get; protected init; }
    public byte[]? BoneInfluenceCounts { get; protected init; }
}

public class PrimitiveData : PrimitiveData<Vector3>;
