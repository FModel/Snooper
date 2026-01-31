using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.UI;

namespace Snooper.Rendering.Containers.Framebuffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, PixelType.Float), IControllable
{
    private const int ScaleRatio = 2;

    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, PixelType.Float);

    private readonly EmbeddedShader _shader = new("Framebuffers/combine.vert", "Framebuffers/ssao.frag");
    private readonly EmbeddedShader _blurShader = new("Framebuffers/combine.vert", "Framebuffers/ssao_blur.frag");

    private int _frameCount;
    private int _directionCount = 6;
    private int _stepsPerDirection = 6;

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
            _shader.SetUniform("uDirectionCount", _directionCount);
            _shader.SetUniform("uStepsPerDirection", _stepsPerDirection);
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

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Ambient Occlusion", () =>
        {
            EditorUI.Property("Direction Count");
            ImGui.DragInt("##Direction Count", ref _directionCount, 0.05f, 1, 6);

            EditorUI.Property("Steps Per Direction");
            ImGui.DragInt("##Steps Per Direction", ref _stepsPerDirection, 0.05f, 1, 6);
        });
    }

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
