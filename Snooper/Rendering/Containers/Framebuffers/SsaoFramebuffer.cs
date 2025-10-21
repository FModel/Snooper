using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float)
{
    private const int ScaleRatio = 2;
    private const int DirectionCount = 6;
    private const int StepsPerDirection = 6;

    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float);

    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/ssao.frag");
    private readonly ShaderProgram _blurShader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/ssao_blur.frag");

    private int _frameCount;

    public override void Generate()
    {
        base.Generate();

        _shader.Generate();
        _shader.Link();

        _blur.Generate();

        _blurShader.Generate();
        _blurShader.Link();
    }

    public override void Bind(TextureUnit unit) => _blur.Bind(unit);

    public void Render(Action<ShaderProgram>? callback = null)
    {
        base.Render(() =>
        {
            _shader.Use();
            callback?.Invoke(_shader);
            _shader.SetUniform("uDirectionCount", DirectionCount);
            _shader.SetUniform("uStepsPerDirection", StepsPerDirection);
            _shader.SetUniform("uFrameCount", ++_frameCount);
            _shader.SetUniform("gPosition", 0);
            _shader.SetUniform("gNormal", 1);
        });

        _blur.Bind();
        GL.ClearColor(1, 1, 1, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _blur.Render(() =>
        {
            base.Bind(TextureUnit.Texture0);

            _blurShader.Use();
            _blurShader.SetUniform("uScaleRatio", ScaleRatio);
            _blurShader.SetUniform("aoInput", 0);
        });
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _blur.Resize(newWidth / ScaleRatio, newHeight / ScaleRatio);
        
        _frameCount = 0;
    }

    public override Texture[] GetTextures() => [.._blur.GetTextures()];
}
