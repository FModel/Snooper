using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public interface IBind
{
    public GetPName Name { get; }
    public int PreviousHandle { get; }

    public void Bind();
    public void Unbind();
}
