using System.Numerics;
using Snooper.Core.Containers.Programs;

namespace Snooper.Rendering.Containers.Buffers;

public class FxaaFramebuffer(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
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

#define FXAA_REDUCE_MIN   (1.0/128.0)
#define FXAA_REDUCE_MUL   (1.0/8.0)
#define FXAA_SPAN_MAX     8.0

in vec2 vTexCoords;

uniform sampler2D combinedTexture;
uniform vec2 inverseScreenSize;

out vec4 FragColor;

vec3 fxaa(sampler2D tex, vec2 uv, vec2 invRes)
{
    vec3 rgbNW = texture(tex, uv + vec2(-1.0, -1.0) * invRes).rgb;
    vec3 rgbNE = texture(tex, uv + vec2(1.0, -1.0) * invRes).rgb;
    vec3 rgbSW = texture(tex, uv + vec2(-1.0, 1.0) * invRes).rgb;
    vec3 rgbSE = texture(tex, uv + vec2(1.0, 1.0) * invRes).rgb;
    vec3 rgbM  = texture(tex, uv).rgb;

    vec3 luma = vec3(0.299, 0.587, 0.114);
    float lumaM  = dot(rgbM,  luma);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);

    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (FXAA_REDUCE_MUL * 0.25), FXAA_REDUCE_MIN);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);

    dir = clamp(dir * rcpDirMin * invRes, -FXAA_SPAN_MAX * invRes, FXAA_SPAN_MAX * invRes);

    vec3 rgbA = 0.5 * (
        texture(tex, uv + dir * (1.0 / 3.0 - 0.5)).rgb +
        texture(tex, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        texture(tex, uv + dir * -0.5).rgb +
        texture(tex, uv + dir * 0.5).rgb);

    float lumaB = dot(rgbB, luma);
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
        return rgbA;
    return rgbB;
}

void main()
{
    FragColor = vec4(fxaa(combinedTexture, vTexCoords, inverseScreenSize), 1.0);
}
""");

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
