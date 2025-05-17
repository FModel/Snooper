using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;

namespace Snooper.Core.Containers;

public class VertexArray(/*ArrayBuffer vertices, ElementArrayBuffer indices*/) : IHandle, IBind
{
    public int Handle { get; private set; }

    public void Generate()
    {
        Handle = GL.GenVertexArray();
    }

    public void Generate(string name)
    {
        Generate();
        GL.ObjectLabel(ObjectLabelIdentifier.VertexArray, Handle, name.Length, name);
    }

    public void Bind()
    {
        GL.BindVertexArray(Handle);
    }

    public void Unbind()
    {
        GL.BindVertexArray(0);
    }

    public bool IsBound() => GL.GetInteger(GetPName.VertexArray) == Handle;

    public void Dispose()
    {
        GL.DeleteVertexArray(Handle);
    }
}
