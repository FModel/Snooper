using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Framebuffers;

public class CombinedFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine");

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
            callback?.Invoke(_shader);
        });
    }
}
