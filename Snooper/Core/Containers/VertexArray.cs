using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers;

public sealed class VertexArray : HandledObject, IBind
{
    public GetPName PName => GetPName.VertexArrayBinding;
    public int PreviousHandle { get; private set; }
    
    public override void Generate()
    {
        GL.CreateVertexArrays(1, out uint handle);
        Handle = handle;
    }
    
    public void Bind()
    {
        PreviousHandle = GL.GetInteger(PName);
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

    public override long Allocated => 0;
    public override long Used => 0;
}
