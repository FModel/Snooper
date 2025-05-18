using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public class VertexArray : Object, IBind
{
    public override GetPName PName { get => GetPName.VertexArray; }

    public override void Generate()
    {
        Handle = GL.GenVertexArray();
    }

    public void Bind()
    {
        GL.BindVertexArray(Handle);
    }

    public void Unbind()
    {
        GL.BindVertexArray(0);
    }

    public override void Dispose()
    {
        GL.DeleteVertexArray(Handle);
    }
}
