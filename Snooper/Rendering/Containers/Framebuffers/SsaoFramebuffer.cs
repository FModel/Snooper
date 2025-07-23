using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight)
    : FullQuadFramebuffer(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float)
{
    private const int NoiseSize = 4;

    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float);
    private readonly Texture2D _ssaoNoise = new(NoiseSize, NoiseSize, PixelInternalFormat.Rgba32f, PixelFormat.Rgb, PixelType.Float);

    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/ssao.frag");
    private readonly ShaderProgram _blurShader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/ssao_blur.frag");

    private readonly Vector3[] _kernel = new Vector3[64];
    private readonly Vector3[] _noise = new Vector3[16];

    public override void Generate()
    {
        base.Generate();

        for (var i = 0; i < _kernel.Length; i++)
        {
            var x = Random.Shared.NextSingle() * 2.0f - 1.0f;
            var y = Random.Shared.NextSingle() * 2.0f - 1.0f;
            var z = Random.Shared.NextSingle();

            var v = Vector3.Normalize(new Vector3(x, y, z));
            v *= Random.Shared.NextSingle();
            var scale = (float) i / _kernel.Length;
            scale = float.Lerp(0.1f, 1.0f, scale * scale);
            v *= scale;

            _kernel[i] = v;
        }

        for (var i = 0; i < _noise.Length; i++)
        {
            var x = Random.Shared.NextSingle() * 2.0f - 1.0f;
            var y = Random.Shared.NextSingle() * 2.0f - 1.0f;
            _noise[i] = Vector3.Normalize(new Vector3(x, y, 0.0f));
        }

        _ssaoNoise.Generate();
        _ssaoNoise.Resize(NoiseSize, NoiseSize, _noise);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);

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
            _ssaoNoise.Bind(TextureUnit.Texture2);

            _shader.Use();
            callback?.Invoke(_shader);
            _shader.SetUniform("noiseScale", new Vector2(Width / NoiseSize, Height / NoiseSize));
            for (var i = 0; i < _kernel.Length; i++)
            {
                _shader.SetUniform($"samples[{i}]", _kernel[i]);
            }
            _shader.SetUniform("gPosition", 0);
            _shader.SetUniform("gNormal", 1);
            _shader.SetUniform("noiseTexture", 2);
        });

        _blur.Bind();
        GL.ClearColor(1, 1, 1, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _blur.Render(() =>
        {
            base.Bind(TextureUnit.Texture0);

            _blurShader.Use();
            _blurShader.SetUniform("ssaoInput", 0);
        });
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _blur.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _blur.GetPointer();
}
