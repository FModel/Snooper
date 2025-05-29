using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem : ActorSystem<GridComponent>
{
    public override uint Order { get; protected set; } = 1;

    private readonly ShaderProgram _shader = new(@"
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

vec3 UnprojectPoint(vec2 xy, float z) {
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
", @"
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

vec4 grid(vec3 fragPos, float scale) {
    vec2 coord = fragPos.xz * scale;
    vec2 derivative = fwidth(coord);
    vec2 grid = abs(fract(coord - 0.5) - 0.5) / derivative;
    float line = min(grid.x, grid.y);
    float minimumz = min(derivative.y, 1) * 0.1;
    float minimumx = min(derivative.x, 1) * 0.1;
    vec4 color = vec4(0.102, 0.102, 0.129, 1.0 - min(line, 1.0));
    if(abs(fragPos.x) < minimumx)
    color.z = 1.0;
    if(abs(fragPos.z) < minimumz)
    color.x = 1.0;
    return color;
}

float computeDepth(vec3 pos) {
    vec4 clip_space_pos = inVar.proj * inVar.view * vec4(pos.xyz, 1.0);
    float clip_space_depth = clip_space_pos.z / clip_space_pos.w;

    float far = gl_DepthRange.far;
    float near = gl_DepthRange.near;

    float depth = (((far-near) * clip_space_depth) + near + far) / 2.0;

    return depth;
}

float computeLinearDepth(vec3 pos) {
    vec4 clip_space_pos = inVar.proj * inVar.view * vec4(pos.xyz, 1.0);
    float clip_space_depth = (clip_space_pos.z / clip_space_pos.w) * 2.0 - 1.0;
    float linearDepth = (2.0 * inVar.near * inVar.far) / (inVar.far + inVar.near - clip_space_depth * (inVar.far - inVar.near));
    return linearDepth / inVar.far;
}

void main() {
    float t = -inVar.nearPoint.y / (inVar.farPoint.y - inVar.nearPoint.y);
    vec3 fragPos3D = inVar.nearPoint + t * (inVar.farPoint - inVar.nearPoint);

    gl_FragDepth = computeDepth(fragPos3D);

    float linearDepth = computeLinearDepth(fragPos3D);
    float fading = max(0, (0.5 - linearDepth));

    FragColor = (grid(fragPos3D, 10) + grid(fragPos3D, 1)) * float(t > 0);
    FragColor.a *= fading;
}
");

    public override void Load()
    {
        _shader.Generate();
        _shader.Link();

        foreach (var component in Components)
        {
            component.Generate();
        }
    }

    public override void Update(float delta)
    {

    }

    public override void Render(CameraComponent camera)
    {
        _shader.Use();
        _shader.SetUniform("view", camera.ViewMatrix);
        _shader.SetUniform("proj", camera.ProjectionMatrix);
        _shader.SetUniform("uNear", camera.NearPlaneDistance);
        _shader.SetUniform("uFar", camera.FarPlaneDistance);

        var bCull = GL.GetBoolean(GetPName.CullFace);
        var bDepth = GL.GetBoolean(GetPName.DepthTest);

        if (bCull) GL.Disable(EnableCap.CullFace);
        if (bDepth) GL.Disable(EnableCap.DepthTest);
        foreach (var component in Components)
        {
            component.Render();
        }
        if (bDepth) GL.Enable(EnableCap.DepthTest);
        if (bCull) GL.Enable(EnableCap.CullFace);
    }
}
