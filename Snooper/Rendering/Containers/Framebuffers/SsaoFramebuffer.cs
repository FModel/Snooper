using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Framebuffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, PixelType.Float)
{
    private const int ScaleRatio = 2;
    private const int DirectionCount = 6;
    private const int StepsPerDirection = 6;

    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, PixelType.Float);

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

    public override void Bind(uint unit) => _blur.Bind(unit);

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
            base.Bind(0);

            _blurShader.Use();
            _blurShader.SetUniform("aoInput", 0);
        });
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth / ScaleRatio, newHeight / ScaleRatio);
        _blur.Resize(newWidth / ScaleRatio, newHeight / ScaleRatio);

        _frameCount = 0;
    }

    public override Texture[] GetTextures() => [.._blur.GetTextures()];

    public override long Allocated
    {
        get
        {
            var total = base.Allocated;
            total += _blur.Allocated;
            total += _shader.Allocated;
            total += _blurShader.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            var total = base.Used;
            total += _blur.Used;
            total += _shader.Used;
            total += _blurShader.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Blur Full Quad Framebuffer", _blur);
        yield return new MemoryDetail("Main Shader", _shader);
        yield return new MemoryDetail("Blur Shader", _blurShader);
    }

    public override void Dispose()
    {
        base.Dispose();

        _blur.Dispose();
        _shader.Dispose();
        _blurShader.Dispose();
    }
}
