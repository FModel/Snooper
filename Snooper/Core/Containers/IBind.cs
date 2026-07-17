using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public interface IBind
{
    public GetPName PName { get; }
    public int PreviousHandle { get; }

    public void Bind();
    public void Unbind();
}

public interface IIndexedBind
{
    public void Bind(uint index);
}
