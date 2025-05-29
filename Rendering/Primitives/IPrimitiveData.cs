using System.Numerics;

namespace Snooper.Rendering.Primitives;

public interface IPrimitiveData
{
    public Vector3[] Vertices { get; }
    public uint[] Indices { get; }
}
