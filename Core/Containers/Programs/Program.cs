using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Programs;

public class Program : HandledObject
{
    public override void Generate()
    {
        Handle = GL.CreateProgram();
    }

    public virtual void Link()
    {
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out var status);
        if (status == 0)
        {
            throw new Exception($"program failed to link with error: {GL.GetProgramInfoLog(Handle)}");
        }
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public override void Dispose()
    {
        GL.DeleteProgram(Handle);
    }
}
