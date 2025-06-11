using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Buffers;

public class PostProcessingFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly ShaderProgram _shader = new(
"""
#version 460 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec2 vTexCoords;

void main()
{
    gl_Position = vec4(aPos, 0.0, 1.0);
    vTexCoords = aTexCoords;
}
""",
"""
#version 460 core

in vec2 vTexCoords;

uniform sampler2D deferredTexture;
uniform sampler2D forwardTexture;

out vec4 FragColor;

void main()
{
    vec4 deferredColor = texture(deferredTexture, vTexCoords);
    vec4 forwardColor = texture(forwardTexture, vTexCoords);

    FragColor = mix(deferredColor, forwardColor, forwardColor.a);
}
""");

    public override void Generate()
    {
        base.Generate();

        _shader.Generate();
        _shader.Link();
    }

    public override void Render(Action<ShaderProgram>? callback = null)
    {
        _shader.Use();
        _shader.SetUniform("deferredTexture", 0);
        _shader.SetUniform("forwardTexture", 1);
        callback?.Invoke(_shader);

        base.Render();
    }
}
