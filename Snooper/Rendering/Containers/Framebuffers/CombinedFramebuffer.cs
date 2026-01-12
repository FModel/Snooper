using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Framebuffers;

public class CombinedFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly EmbeddedShader _shader = new("Framebuffers/combine");

    public override void Generate()
    {
        base.Generate();

        _shader.Generate();
        _shader.Link();
    }

    public void Render(Action<ShaderProgram>? callback = null)
    {
        base.Render(() =>
        {
            _shader.Use();
            _shader.SetUniform("deferredTexture", 0);
            _shader.SetUniform("forwardTexture", 1);
            _shader.SetUniform("outlineTexture", 2);
            callback?.Invoke(_shader);
        });
    }

    public override long Allocated => base.Allocated + _shader.Allocated;
    public override long Used => base.Used + _shader.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Main Shader", _shader);
    }

    public override void Dispose()
    {
        base.Dispose();

        _shader.Dispose();
    }
}
