using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public abstract class Object : IHandle
{
    public int Handle { get; protected set; }
    public abstract GetPName PName { get; }

    public abstract void Generate();

    public abstract void Dispose();

    protected bool CanExecute()
    {
#if DEBUG
        var current = GL.GetInteger(PName);
        return current == 0 || (Handle > 0 && current == Handle);
#else
        return true;
#endif
    }
}
