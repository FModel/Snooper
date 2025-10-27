namespace Snooper.Core.Containers;

public abstract class HandledObject : IMemorySizeProvider, IDisposable
{
    protected int Handle { get; set; }

    public abstract void Generate();
    public abstract void Dispose();
    
    public abstract long Allocated { get; }
    public abstract long Used { get; }

    public static implicit operator int(HandledObject @object) => @object.Handle;
}
