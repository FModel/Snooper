namespace Snooper.Core.Containers;

public abstract class HandledObject : IMemorySizeProvider, IDisposable
{
    protected uint Handle { get; set; }

    public abstract void Generate();
    public abstract void Dispose();
    
    public abstract long Allocated { get; }
    public abstract long Used { get; }

    public static implicit operator uint(HandledObject @object) => @object.Handle;
}
