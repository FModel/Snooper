namespace Snooper.Core.Containers;

public abstract class ArbHandledObject : IDisposable
{
    protected long ArbHandle { get; set; }

    public abstract void Generate();
    public abstract void Dispose();

    public static implicit operator ulong(ArbHandledObject @object) => (ulong)@object.ArbHandle;
}
