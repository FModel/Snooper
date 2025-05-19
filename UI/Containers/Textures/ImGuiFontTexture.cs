using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Snooper.UI.Containers.Textures;

public class ImGuiFontTexture() : Texture2D(0, 0)
{
    public override void Generate()
    {
        // save previous state
        var pActiveTexture = GL.GetInteger(GetPName.ActiveTexture);
        var pTexture2D = GL.GetInteger(GetPName.TextureBinding2D);

        {
            var io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out _);
            var mips = (int) Math.Floor(Math.Log(Math.Max(width, height), 2));

            base.Generate();
            Bind(TextureUnit.Texture0);

            GL.TexStorage2D(TextureTarget2d.Texture2D, mips, SizedInternalFormat.Rgba8, width, height);
            GL.TexSubImage2D(Target, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
            GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, mips - 1);

            io.Fonts.SetTexID(Handle);
            io.Fonts.ClearTexData();
        }

        // restore previous state
        GL.BindTexture(TextureTarget.Texture2D, pTexture2D);
        GL.ActiveTexture((TextureUnit) pActiveTexture);
    }
}
