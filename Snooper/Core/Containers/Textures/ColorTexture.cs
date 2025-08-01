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
        
        Bind();
        
        var c = _color.Value;
        GL.TexImage2D(Target, 0, info.InternalFormat, Width, Height, 0, info.Format, info.Type, ref c);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        
        OnTextureReadyForBindless();
    }
}