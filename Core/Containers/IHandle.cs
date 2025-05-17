namespace Snooper.Core.Containers;

public interface IHandle : IDisposable
{
    public int Handle { get; }

    public void Generate();
    public void Generate(string name);
}
