using System.Numerics;

namespace Snooper.Rendering.Primitives;

public interface TPrimitiveData<T> where T : unmanaged
{
    public T[] Vertices { get; }
    public uint[] Indices { get; }
    
    public bool IsValid => Vertices.Length > 0 && Indices.Length > 0;
}

public interface IPrimitiveData : TPrimitiveData<Vector3>;
