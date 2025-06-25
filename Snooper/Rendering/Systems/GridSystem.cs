using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem : PrimitiveSystem<GridComponent>
{
    public override uint Order => 1;

    protected override ShaderProgram Shader { get; } = new(
"""
#version 460 core

layout (location = 0) in vec3 vPos;

// --------------------- OUT ---------------------
out OUT_IN_VARIABLES {
    vec3 nearPoint;
    vec3 farPoint;
    mat4 proj;
    mat4 view;
    float near;
    float far;
} outVar;

uniform mat4 proj;
uniform mat4 view;
uniform float uNear;
uniform float uFar;

vec3 UnprojectPoint(vec2 xy, float z)
{
    mat4 viewInv = inverse(view);
    mat4 projInv = inverse(proj);
    vec4 unprojectedPoint =  viewInv * projInv * vec4(xy, z, 1.0);
    return unprojectedPoint.xyz / unprojectedPoint.w;
}

void main()
{
    outVar.near = uNear;
    outVar.far  = uFar;
    outVar.proj = proj;
    outVar.view = view;
    outVar.nearPoint = UnprojectPoint(vPos.xy, -1.0).xyz;
    outVar.farPoint  = UnprojectPoint(vPos.xy, 1.0).xyz;
    gl_Position = vec4(vPos, 1.0);
}
""",
"""
#version 460 core

// --------------------- IN ---------------------
in OUT_IN_VARIABLES {
    vec3 nearPoint;
    vec3 farPoint;
    mat4 proj;
    mat4 view;
    float near;
    float far;
} inVar;

// --------------------- OUT --------------------
out vec4 FragColor;

vec4 grid(vec3 fragPos, float scale)
{
    vec2 coord = fragPos.xz * scale;
    vec2 derivative = fwidth(coord);
    vec2 grid = abs(fract(coord - 0.5) - 0.5) / derivative;
    float line = min(grid.x, grid.y) / 2.0;
    float minimumz = min(derivative.y, 1) * 0.1;
    float minimumx = min(derivative.x, 1) * 0.1;
    vec4 color = vec4(0.1, 0.1, 0.1, 1.0 - min(line, 0.75));
    if(abs(fragPos.x) < minimumx)
    color.z = 1.0;
    if(abs(fragPos.z) < minimumz)
    color.x = 1.0;
    return color;
}

float computeDepth(vec3 pos)
{
    vec4 clip_space_pos = inVar.proj * inVar.view * vec4(pos.xyz, 1.0);
    float clip_space_depth = clip_space_pos.z / clip_space_pos.w;

    float far = gl_DepthRange.far;
    float near = gl_DepthRange.near;

    float depth = (((far-near) * clip_space_depth) + near + far) / 2.0;

    return depth;
}

float computeLinearDepth(vec3 pos)
{
    vec4 clip_space_pos = inVar.proj * inVar.view * vec4(pos.xyz, 1.0);
    float clip_space_depth = (clip_space_pos.z / clip_space_pos.w) * 2.0 - 1.0;
    float linearDepth = (2.0 * inVar.near * inVar.far) / (inVar.far + inVar.near - clip_space_depth * (inVar.far - inVar.near));
    return linearDepth / inVar.far / 2.0;
}

void main()
{
    float t = -inVar.nearPoint.y / (inVar.farPoint.y - inVar.nearPoint.y);
    vec3 fragPos3D = inVar.nearPoint + t * (inVar.farPoint - inVar.nearPoint);

    gl_FragDepth = computeDepth(fragPos3D);

    float linearDepth = computeLinearDepth(fragPos3D);
    float fading = max(0, (0.5 - linearDepth));

    FragColor = (grid(fragPos3D, 10) + grid(fragPos3D, 1)) * float(t > 0);
    FragColor.a *= fading;
}
""");

    protected override void PreRender(CameraComponent camera)
    {
        Shader.Use();
        Shader.SetUniform("view", camera.ViewMatrix);
        Shader.SetUniform("proj", camera.ProjectionMatrix);
        Shader.SetUniform("uNear", camera.NearPlaneDistance);
        Shader.SetUniform("uFar", camera.FarPlaneDistance);
        
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera)
    {
        GL.DepthMask(true);
    }
}
