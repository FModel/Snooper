using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class MsaaFramebuffer(int originalWidth, int originalHeight) : Framebuffer
{
    private readonly PostProcFramebuffer _postProcFramebuffer = new(originalWidth, originalHeight, new ShaderProgram(@"
#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec2 vTexCoords;

void main()
{
    gl_Position = vec4(aPos, 0.0, 1.0);
    vTexCoords = aTexCoords;
}
", @"
#version 330 core

in vec2 vTexCoords;

out vec4 FragColor;

uniform sampler2D screenTexture;
uniform vec2 resolution; // The resolution of the texture
uniform float aberrationStrength; // Strength of the chromatic aberration effect

void main() {
    vec2 texCoord = vTexCoords;

    // Calculate the offset for red and blue channels
    vec2 offset = (texCoord - vec2(0.5, 0.5)) * (2.0 / resolution) * aberrationStrength;

    // Sample each color channel with a slight offset
    float r = texture(screenTexture, texCoord + offset).r;
    float g = texture(screenTexture, texCoord).g;
    float b = texture(screenTexture, texCoord - offset).b;

    // Combine the color channels
    FragColor = vec4(r, g, b, 1.0);
}
"));
    private readonly Texture2DMultisample _color = new(originalWidth, originalHeight);
    private readonly Renderbuffer _depth = new(originalWidth, originalHeight, RenderbufferStorage.Depth24Stencil8, true);

    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);

        _depth.Generate();
        _depth.Resize(originalWidth, originalHeight);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, _color.Target, _color, 0);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);

        CheckStatus();

        _postProcFramebuffer.Generate();
    }

    public void RenderPostProcessing()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Handle);
        _postProcFramebuffer.Render(shader =>
        {
            shader.SetUniform("screenTexture", 0);
            shader.SetUniform("resolution", new Vector2(_color.Width, _color.Height));
            shader.SetUniform("aberrationStrength", 5f); // Adjust this value for the desired effect strength
            _color.Bind(TextureUnit.Texture0);
        });
    }

    public override void Resize(int newWidth, int newHeight)
    {
        _postProcFramebuffer.Resize(newWidth, newHeight);
        _color.Resize(newWidth, newHeight);
        _depth.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _postProcFramebuffer.GetPointer();
}
