#extension GL_ARB_bindless_texture : require

layout (quads, fractional_odd_spacing, ccw) in;

struct PerInstanceData
{
    mat4 Matrix;
};

struct PerDrawData
{
    bool IsReady;
    sampler2D Heightmap;
    vec2 ScaleBias;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

layout(std430, binding = 2) restrict readonly buffer LandscapeScales
{
    vec2 uLandscapeScales[];
};

in flat int tcInstanceIndex[];
in flat int tcDrawIndex[];

uniform float uSizeQuads;
uniform float uQuadCount;
uniform float uGlobalScale;
uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out TE_OUT {
    vec3 vViewPos;
    float vHeight;
    mat3 TBN;
} te_out;

void main()
{
    float u = gl_TessCoord.x;
    float v = gl_TessCoord.y;
    
    vec4 p00 = gl_in[0].gl_Position;
    vec4 p01 = gl_in[1].gl_Position;
    vec4 p10 = gl_in[2].gl_Position;
    vec4 p11 = gl_in[3].gl_Position;

    vec4 p0 = (p01 - p00) * u + p00;
    vec4 p1 = (p11 - p10) * u + p10;
    vec4 p = (p1 - p0) * v + p0;

    vec4 normal = normalize(vec4(cross((p10 - p00).xyz, (p01 - p00).xyz), 0));
    mat4 matrix = uViewMatrix * uInstanceDataBuffer[tcInstanceIndex[0]].Matrix;
    
    PerDrawData drawData = uDrawDataBuffer[tcDrawIndex[0]];
    if (!drawData.IsReady)
    {
        te_out.vViewPos = vec3(0.0);
        te_out.vHeight = 0.0;
        te_out.TBN = mat3(1.0);
        gl_Position = uProjectionMatrix * matrix * p;
        return;
    }
    
    vec2 heightmapSize = textureSize(drawData.Heightmap, 0);
    vec2 texelSize = 1.0 / heightmapSize;
    vec2 componentUvSize = vec2(uSizeQuads) / heightmapSize;
    float quadFraction = 1.0 / uQuadCount;

    vec2 subPatchOffset = uLandscapeScales[gl_PrimitiveID] * quadFraction;
    vec2 uv = drawData.ScaleBias + subPatchOffset * componentUvSize + vec2(u, v) * (componentUvSize * quadFraction);
    uv = uv * (1.0 - texelSize) + 0.5 * texelSize;

    vec4 color = texture(drawData.Heightmap, uv);
    float R = color.r * 255.0;
    float G = color.g * 255.0;
    te_out.vHeight = ((R * 256.0) + G - 32768.0) / 128.0 * uGlobalScale;

    float nx = 2.0 * color.a - 1.0;
    float nz = 2.0 * color.b - 1.0;
    float ny = sqrt(1.0 - nx * nx + nz * nz);
    te_out.TBN = mat3(normalize(vec3(-nz, 0.0, nx)), normalize(vec3(0.0, nz, -ny)), normalize(vec3(nx, ny, nz)));

    // displace point along normal
    p += normal * te_out.vHeight;

    vec4 viewPos = matrix * p;
    gl_Position = uProjectionMatrix * viewPos;
    te_out.vViewPos = viewPos.xyz;
}