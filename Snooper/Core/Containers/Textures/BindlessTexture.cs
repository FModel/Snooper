using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class BindlessTexture(Texture texture) : ArbHandledObject
{
    public override void Generate()
    {
        ArbHandle = GL.Arb.GetTextureHandle(texture);
    }
    
    public void MakeResident()
    {
        if (!IsResident())
        {
            GL.Arb.MakeTextureHandleResident(ArbHandle);
        }
    }
    
    public void MakeNonResident()
    {
        if (IsResident())
        {
            GL.Arb.MakeTextureHandleNonResident(ArbHandle);
        }
    }

    private bool IsResident() => GL.Arb.IsTextureHandleResident(ArbHandle);
    
    public override void Dispose()
    {
        MakeNonResident();
    }
}