using CUE4Parse.UE4.Objects.Core.Math;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core.Containers.Textures;

public class ColorTexture(FColor color) : Texture2D(1, 1)
{
    private readonly FColor? _color = color;

    public ColorTexture(FLinearColor color) : this(color.ToFColor(false))
    {
        
    }

    public override void Generate()
    {
        base.Generate();
        if (_color is null || FormatInfo is not TextureFormatInfo info) return;
        
        var c = _color.Value;
        GL.TextureStorage2D(Handle, 1, info.InternalFormat, Width, Height);
        GL.TextureSubImage2D(Handle, 0, 0, 0, Width, Height, info.Format, info.Type, ref c);
        
        GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        
        OnTextureReadyForBindless();
    }
}