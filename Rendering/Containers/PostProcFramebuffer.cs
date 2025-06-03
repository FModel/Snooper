using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers;

public class PostProcFramebuffer(int originalWidth, int originalHeight) : Framebuffer, IResizable
{
    private readonly Texture2D _color = new(originalWidth, originalHeight);
    
    private readonly VertexArray _vao = new();
    private readonly ArrayBuffer<Vector4> _vbo = new(4, BufferUsageHint.StaticDraw);
    private readonly ElementArrayBuffer<uint> _ebo = new(6, BufferUsageHint.StaticDraw);
    private readonly ShaderProgram _shader = new(@"
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
");
    
    public override void Generate()
    {
        _color.Generate();
        _color.Resize(originalWidth, originalHeight);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        base.Bind();
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);

        CheckStatus();
        
        _vao.Generate();
        _vao.Bind();

        _vbo.Generate();
        _vbo.Bind();
        _vbo.SetData(new Vector4[]
        {
            new(1.0f, -1.0f, 1.0f, 0.0f),
            new(-1.0f, -1.0f, 0.0f, 0.0f),
            new(-1.0f, 1.0f, 0.0f, 1.0f),
            new(1.0f, 1.0f, 1.0f, 1.0f),
        });

        _ebo.Generate();
        _ebo.Bind();
        _ebo.SetData(new uint[] { 0, 1, 2, 3, 0, 2 });

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, _vbo.Stride, 8);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        
        _shader.Generate();
        _shader.Link();
    }

    public void RenderPostProcessing()
    {
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Handle);
        GL.BlitFramebuffer(0, 0, _color.Width, _color.Height, 0, 0, _color.Width, _color.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        
        // --- POST PROCESS EFFECTS ---
        // _shader.Use();
        // _shader.SetUniform("screenTexture", 0);
        // _shader.SetUniform("resolution", new Vector2(_color.Width, _color.Height));
        // _shader.SetUniform("aberrationStrength", 5f); // Adjust this value for the desired effect strength
        // _color.Bind(TextureUnit.Texture0);
        //
        // _vao.Bind();
        // GL.DrawElements(PrimitiveType.Triangles, _ebo.Size, DrawElementsType.UnsignedInt, 0);
    }

    public void Resize(int newWidth, int newHeight)
    {
        _color.Resize(newWidth, newHeight);
    }

    public IntPtr GetPointer() => _color.GetPointer();
}