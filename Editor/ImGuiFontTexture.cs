using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Textures;

namespace Editor;

public class ImGuiFontTexture() : Texture2D(0, 0, SizedInternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.UnsignedByte)
{
    public override void Generate()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var width, out var height);

        base.Generate();
        if (FormatInfo is not TextureFormatInfo info) return;

        Width = width;
        Height = height;

        var mipCount = (int)Math.Floor(Math.Log2(Math.Max(Width, Height))) + 1;
        GL.TextureStorage2D(Handle, mipCount, info.InternalFormat, Width, Height);
        GL.TextureSubImage2D(Handle, 0, 0, 0, Width, Height, info.Format, info.Type, pixels);
        GL.GenerateTextureMipmap(Handle);

        SetSampling(TextureMinFilter.Linear, TextureMagFilter.Linear, TextureWrapMode.Repeat);
        GL.TextureParameter(Handle, TextureParameterName.TextureBaseLevel, 0);
        GL.TextureParameter(Handle, TextureParameterName.TextureMaxLevel, mipCount - 1);

        io.Fonts.SetTexID(GetPointer());
        io.Fonts.ClearTexData();
    }
}
