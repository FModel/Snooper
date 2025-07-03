namespace Snooper.Core.Containers;

public abstract class ArbHandledObject : IDisposable
{
    protected long Handle { get; set; }

    public abstract void Generate();
    public abstract void Dispose();

    public static implicit operator long(ArbHandledObject @object) => @object.Handle;
}
