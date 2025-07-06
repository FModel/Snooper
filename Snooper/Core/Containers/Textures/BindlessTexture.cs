using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class BindlessTexture(Texture texture) : ArbHandledObject
{
    public override void Generate()
    {
        texture.Generate();
        Handle = GL.Arb.GetTextureHandle(texture);
    }
    
    public void MakeResident()
    {
        if (!IsResident)
        {
            GL.Arb.MakeTextureHandleResident(Handle);
        }
    }
    
    public void MakeNonResident()
    {
        if (IsResident)
        {
            GL.Arb.MakeTextureHandleNonResident(Handle);
        }
    }
    
    public bool IsResident => GL.Arb.IsTextureHandleResident(Handle);
    
    public override void Dispose()
    {
        MakeNonResident();
        texture.Dispose();
    }
}