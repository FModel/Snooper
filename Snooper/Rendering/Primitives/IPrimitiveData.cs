using System.Numerics;

namespace Snooper.Rendering.Primitives;

public interface TPrimitiveData<T> : IDisposable where T : unmanaged
{
    public T[]? Vertices { get; }
    public uint[]? Indices { get; }
    
    public bool IsValid => Vertices?.Length > 0 && Indices?.Length > 0;
}

public abstract class PrimitiveData<T> : TPrimitiveData<T> where T : unmanaged
{
    public T[]? Vertices { get; protected set; }
    public uint[]? Indices { get; protected set; }

    public void Dispose()
    {
        if (Vertices is not null)
        {
            Array.Clear(Vertices);
            Vertices = null;
        }
        
        if (Indices is not null)
        {
            Array.Clear(Indices);
            Indices = null;
        }
    }
}

public class PrimitiveData : PrimitiveData<Vector3>;
