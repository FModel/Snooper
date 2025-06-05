using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;

namespace Snooper.Rendering.Containers.Buffers;

public class SsaoFramebuffer(int originalWidth, int originalHeight)
    : FullQuadFramebuffer(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float)
{
    private readonly FullQuadFramebuffer _blur = new(originalWidth, originalHeight, PixelInternalFormat.R8, PixelFormat.Red, PixelType.Float);
    private readonly Texture2D _noiseTexture = new(4, 4, PixelInternalFormat.Rgba32f, PixelFormat.Rgb, PixelType.Float);

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
uniform mat4 uProjectionMatrix;

out float FragColor;

int kernelSize = 64;
float radius = 0.5;
float bias = 0.025;

void main()
{
    vec3 fragPos = texture(gPosition, vTexCoords).xyz;
    vec3 normal = normalize(texture(gNormal, vTexCoords).rgb);
    vec3 randomVec = normalize(texture(noiseTexture, vTexCoords * noiseScale).xyz);
    
    // create TBN change-of-basis matrix: from tangent-space to view-space
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);
    
    // iterate over the sample kernel and calculate occlusion factor
    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {
        // get sample position
        vec3 samplePos = TBN * samples[i]; // from tangent to view-space
        samplePos = fragPos + samplePos * radius; 
        
        // project sample position (to sample texture) (to get position on screen/texture)
        vec4 offset = vec4(samplePos, 1.0);
        offset = uProjectionMatrix * offset; // from view to clip-space
        offset.xyz /= offset.w; // perspective divide
        offset.xyz = offset.xyz * 0.5 + 0.5; // transform to range 0.0 - 1.0
        
        // get sample depth
        float sampleDepth = texture(gPosition, offset.xy).z; // get depth value of kernel sample
        
        // range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;           
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
    float result = 0.0;
    for (int x = -2; x < 2; ++x) 
    {
        for (int y = -2; y < 2; ++y) 
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
            _kernel[i] = Vector3.Normalize(new Vector3(x, y, z));
        }
        
        for (var i = 0; i < _noise.Length; i++)
        {
            var x = Random.Shared.NextSingle() * 2.0f - 1.0f;
            var y = Random.Shared.NextSingle() * 2.0f - 1.0f;
            _noise[i] = new Vector3(x, y, 0.0f);
        }
        
        _noiseTexture.Generate();
        _noiseTexture.Resize(4, 4, _noise);
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

    public override void Bind()
    {
        base.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

    public override void Render()
    {
        _noiseTexture.Bind(TextureUnit.Texture2);
        
        _shader.Use();
        _shader.SetUniform("noiseScale", new Vector2(Width / _noiseTexture.Width, Height / _noiseTexture.Height));
        for (var i = 0; i < _kernel.Length; i++)
        {
            _shader.SetUniform($"samples[{i}]", _kernel[i]);
        }
        _shader.SetUniform("gPosition", 0);
        _shader.SetUniform("gNormal", 1);
        _shader.SetUniform("noiseTexture", 2);
    }

    public void RenderBlur(Matrix4x4 projectionMatrix)
    {
        _shader.SetUniform("uProjectionMatrix", projectionMatrix);
        base.Render();
        
        // _blur.Bind();
        // GL.ClearColor(0, 0, 0, 0);
        // GL.Clear(ClearBufferMask.ColorBufferBit);
        //
        // _blur.Bind(TextureUnit.Texture0);
        //
        // _blurShader.Use();
        // _blurShader.SetUniform("ssaoInput", 0);
        //
        // _blur.Render();
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        _blur.Resize(newWidth, newHeight);
    }

    // public override IntPtr GetPointer() => _blur.GetPointer();
}