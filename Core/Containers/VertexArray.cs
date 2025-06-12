using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public sealed class VertexArray : HandledObject, IBind
{
    public GetPName Name => GetPName.VertexArrayBinding;
    public int PreviousHandle { get; private set; }

    public override void Generate()
    {
        Handle = GL.GenVertexArray();
    }

    public void Bind()
    {
        PreviousHandle = GL.GetInteger(Name);
        GL.BindVertexArray(Handle);
    }

    public void Unbind()
    {
        GL.BindVertexArray(PreviousHandle);
    }

    public override void Dispose()
    {
        GL.DeleteVertexArray(Handle);
    }
}
