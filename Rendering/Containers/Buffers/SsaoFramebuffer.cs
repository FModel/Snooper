using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight)
    : FullQuadFramebuffer(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float)
{
    private const int NoiseSize = 4;

    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float);
    private readonly Texture2D _ssaoNoise = new(NoiseSize, NoiseSize, PixelInternalFormat.Rgba32f, PixelFormat.Rgb, PixelType.Float);

    private readonly ShaderProgram _shader = new(
"""
#version 330 core
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
#version 330 core

in vec2 vTexCoords;

uniform vec2 noiseScale;
uniform vec3 samples[64];
uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D noiseTexture;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform float radius;
uniform float bias;

out float FragColor;

int kernelSize = 64;

void main()
{
    vec3 fragPos = vec3(uViewMatrix * texture(gPosition, vTexCoords));
    vec3 normal = texture(gNormal, vTexCoords).xyz;
    vec3 randomVec = texture(noiseTexture, vTexCoords * noiseScale).xyz;

    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    mat3 tbn = mat3(tangent, cross(normal, tangent), normal);

    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {
        vec3 samplePos = tbn * samples[i];
        samplePos = fragPos + samplePos * radius;

        vec4 offset = vec4(samplePos, 1.0);
        offset = uProjectionMatrix * offset;
        offset.xyz /= offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5;

        float sampleDepth = vec3(uViewMatrix * texture(gPosition, offset.xy)).z;

        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth <= samplePos.z - bias ? 1.0 : 0.0) * rangeCheck;
    }

    FragColor = 1.0 - (occlusion / kernelSize);
}
""");
    private readonly ShaderProgram _blurShader = new(
"""
#version 330 core
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
#version 330 core

in vec2 vTexCoords;

uniform sampler2D ssaoInput;

out float FragColor;

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(ssaoInput, 0));
    int intensity = 2;

    float result = 0.0;
    for (int x = -intensity; x < intensity; ++x)
    {
        for (int y = -intensity; y < intensity; ++y)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            result += texture(ssaoInput, vTexCoords + offset).r;
        }
    }

    FragColor = result / (4.0 * 4.0);
}
""");

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
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

        _shader.Generate();
        _shader.Link();

        _blur.Generate();

        _blurShader.Generate();
        _blurShader.Link();
    }

    public override void Bind(TextureUnit unit) => _blur.Bind(unit);

    public override void Render(Action<ShaderProgram>? callback = null)
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

        base.Render();
        base.Bind(TextureUnit.Texture0);

        _blur.Bind();
        GL.ClearColor(255, 255, 255, 255);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _blurShader.Use();
        _blurShader.SetUniform("ssaoInput", 0);

        _blur.Render();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _blur.Resize(newWidth, newHeight);
    }

    public override IntPtr GetPointer() => _blur.GetPointer();
}
