using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem() : PrimitiveSystem<GridComponent>(1)
{
    public override uint Order => 99;
    protected override bool AllowDerivation => true;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("grid");

    private Texture? _texture;

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        
        shader.SetUniform("uNear", camera.NearPlaneDistance);
        shader.SetUniform("uFar", camera.FarPlaneDistance);
        shader.SetUniform("uIsOpaque", _texture is not null);
        _texture?.Bind(TextureUnit.Texture0);
        shader.SetUniform("uTexture", 0);
        
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PostRender(camera, shader);
        
        _texture?.Unbind();
        GL.DepthMask(true);
    }

    protected override void OnActorComponentAdded(GridComponent component)
    {
        base.OnActorComponentAdded(component);

        if (component is OpaqueGridComponent && _texture is null)
        {
            _texture = new EmbeddedTexture2D("Rendering.Resources.grid.png");
            _texture.Generate();
            GL.TexParameter(_texture.Target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureMaxLevel, 8);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureWrapR, (int) TextureWrapMode.Repeat);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
            GL.TexParameter(_texture.Target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            _texture.Unbind();
        }
    }
}
