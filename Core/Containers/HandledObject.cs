namespace Snooper.Core.Containers;

public abstract class HandledObject : IDisposable
{
    protected int Handle { get; set; }

    public abstract void Generate();
    public abstract void Dispose();

    public static implicit operator int(HandledObject @object) => @object.Handle;
}
