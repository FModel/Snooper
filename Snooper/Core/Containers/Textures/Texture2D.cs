using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace Snooper.Core.Containers.Textures;

public class Texture2D(int width, int height,
    PixelInternalFormat internalFormat = PixelInternalFormat.Rgba,
    PixelFormat format = PixelFormat.Rgba,
    PixelType type = PixelType.UnsignedByte)
    : Texture(width, height, TextureTarget.Texture2D, internalFormat, format, type)
{
    public override GetPName Name => GetPName.TextureBinding2D;

    private readonly UTexture? _owner;

    public Texture2D(UTexture texture) : this(texture.PlatformData.SizeX, texture.PlatformData.SizeY)
    {
        _owner = texture;
        
        Guid = texture.LightingGuid;
    }

    public override void Generate()
    {
        if (_owner is null)
        {
            base.Generate();
            return;
        }

        Task.Run(() =>
        {
            Log.Debug("Decoding texture {Name} with format {Format} of size {Width}x{Height}.", _owner.Name, _owner.Format, Width, Height);
            
            var decoded = _owner.Decode();
            if (decoded is null)
                throw new InvalidOperationException("Failed to decode texture data.");
            
            MainThreadDispatcher.Enqueue(() =>
            {
                base.Generate();
                
                Bind();
                Resize(decoded);
                Log.Debug("Texture {Name} uploaded to GPU.", _owner.Name);
        
                if (_owner.LODGroup is TextureGroup.TEXTUREGROUP_Terrain_Heightmap or TextureGroup.TEXTUREGROUP_Terrain_Weightmap)
                {
                    GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
                    GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
                    GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
                    GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
                }
                else
                {
                    GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
                    GL.TexParameter(Target, TextureParameterName.TextureBaseLevel, 0);
                    GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, 8);
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                }
                
                OnTextureReadyForBindless();
            });
        });
    }
}
