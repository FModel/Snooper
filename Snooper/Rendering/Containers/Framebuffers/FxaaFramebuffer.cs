using System.Numerics;
using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Framebuffers;

public class FxaaFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly ShaderProgram _shader = new EmbeddedShaderProgram("Framebuffers/combine.vert", "Framebuffers/fxaa.frag");

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
            _shader.SetUniform("combinedTexture", 0);
            _shader.SetUniform("inverseScreenSize", new Vector2(1.0f / Width, 1.0f / Height));
            callback?.Invoke(_shader);
        });
    }
}
