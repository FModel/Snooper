using OpenTK.Graphics.OpenGL4;
using Snooper.UI;

namespace Snooper.Core.Containers.Textures;

public class BindlessTexture(Texture texture) : ArbHandledObject, IControllable
{
    public override void Generate()
    {
        if (ArbHandle > 0)
            throw new InvalidOperationException("Bindless texture already generated.");

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

    public IntPtr GetPointer() => texture.GetPointer();

    public void DrawControls() => texture.DrawControls();

    public override void Dispose()
    {
        if (ArbHandle > 0)
            MakeNonResident();
    }
}
