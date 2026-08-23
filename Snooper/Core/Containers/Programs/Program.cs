using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Programs;

public abstract class Program : HandledObject, ICloneable
{
    public override void Generate()
    {
        Handle = (uint)GL.CreateProgram();
    }

    public virtual void Link()
    {
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out var status);
        if (status == 0)
        {
            throw new Exception($"program failed to link with error: {GL.GetProgramInfoLog((int)Handle)}");
        }
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public void Unuse()
    {
        GL.UseProgram(0);
    }

    [Conditional("DEBUG")]
    protected void VerifyCurrent()
    {
        if (Handle != GL.GetInteger(GetPName.CurrentProgram))
            throw new Exception("program is not current");
    }

    public abstract object Clone();

    public override void Dispose()
    {
        GL.DeleteProgram(Handle);
    }
}
