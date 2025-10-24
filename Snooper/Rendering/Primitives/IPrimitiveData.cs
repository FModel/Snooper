using System.Numerics;

namespace Snooper.Rendering.Primitives;

public interface TPrimitiveData<T> : IDisposable where T : unmanaged
{
    public T[]? Vertices { get; }
    public uint[]? Indices { get; }
    
    // optional data
    public int[]? Colors { get; }
    public Vector2[]? ExtraUvs { get; } // we support one extra UV set for now, TODO: actually implement this
}

public abstract class PrimitiveData<T> : TPrimitiveData<T> where T : unmanaged
{
    public T[]? Vertices { get; protected set; }
    public uint[]? Indices { get; protected set; }
    
    public int[]? Colors { get; protected set; }
    public Vector2[]? ExtraUvs { get; protected set; }

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
        
        if (Colors is not null)
        {
            Array.Clear(Colors);
            Colors = null;
        }
        
        if (ExtraUvs is not null)
        {
            Array.Clear(ExtraUvs);
            ExtraUvs = null;
        }
    }
}

public class PrimitiveData : PrimitiveData<Vector3>;
