using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

/// <summary>
/// https://github.com/wwwtyro/glsl-atmosphere
/// </summary>
public class SkyboxSystem() : PrimitiveSystem<CubeComponent>(1)
{
    public override uint Order => 1;
    protected override ShaderProgram Shader { get; }  = new(
"""
#version 460 core
layout (location = 0) in vec3 aPos;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out vec3 vTexCoords;

void main()
{
    vTexCoords = aPos;
    gl_Position = (uProjectionMatrix * uViewMatrix * vec4(aPos, 1.0)).xyww;
}
""",
"""
#version 460 core

#define PI 3.141592
#define iSteps 16
#define jSteps 8

in vec3 vTexCoords;

uniform vec3 uPlanetPos;
uniform float uPlanetIntensity;
uniform float uPlanetRadius;
uniform float uPlanetAtmosphereRadius;

out vec4 FragColor;

vec2 rsi(vec3 r0, vec3 rd, float sr)
{
    // ray-sphere intersection that assumes
    // the sphere is centered at the origin.
    // No intersection when result.x > result.y
    float a = dot(rd, rd);
    float b = 2.0 * dot(rd, r0);
    float c = dot(r0, r0) - (sr * sr);
    float d = (b*b) - 4.0*a*c;
    if (d < 0.0) return vec2(1e5,-1e5);
    return vec2(
        (-b - sqrt(d))/(2.0*a),
        (-b + sqrt(d))/(2.0*a)
    );
}

vec3 atmosphere(vec3 r, vec3 r0, vec3 pSun, float iSun, float rPlanet, float rAtmos, vec3 kRlh, float kMie, float shRlh, float shMie, float g)
{
    // Normalize the sun and view directions.
    pSun = normalize(pSun);
    r = normalize(r);

    // Calculate the step size of the primary ray.
    vec2 p = rsi(r0, r, rAtmos);
    if (p.x > p.y) return vec3(0,0,0);
    p.y = min(p.y, rsi(r0, r, rPlanet).x);
    float iStepSize = (p.y - p.x) / float(iSteps);

    // Initialize the primary ray time.
    float iTime = 0.0;

    // Initialize accumulators for Rayleigh and Mie scattering.
    vec3 totalRlh = vec3(0,0,0);
    vec3 totalMie = vec3(0,0,0);

    // Initialize optical depth accumulators for the primary ray.
    float iOdRlh = 0.0;
    float iOdMie = 0.0;

    // Calculate the Rayleigh and Mie phases.
    float mu = dot(r, pSun);
    float mumu = mu * mu;
    float gg = g * g;
    float pRlh = 3.0 / (16.0 * PI) * (1.0 + mumu);
    float pMie = 3.0 / (8.0 * PI) * ((1.0 - gg) * (mumu + 1.0)) / (pow(1.0 + gg - 2.0 * mu * g, 1.5) * (2.0 + gg));

    // Sample the primary ray.
    for (int i = 0; i < iSteps; i++) {

        // Calculate the primary ray sample position.
        vec3 iPos = r0 + r * (iTime + iStepSize * 0.5);

        // Calculate the height of the sample.
        float iHeight = length(iPos) - rPlanet;

        // Calculate the optical depth of the Rayleigh and Mie scattering for this step.
        float odStepRlh = exp(-iHeight / shRlh) * iStepSize;
        float odStepMie = exp(-iHeight / shMie) * iStepSize;

        // Accumulate optical depth.
        iOdRlh += odStepRlh;
        iOdMie += odStepMie;

        // Calculate the step size of the secondary ray.
        float jStepSize = rsi(iPos, pSun, rAtmos).y / float(jSteps);

        // Initialize the secondary ray time.
        float jTime = 0.0;

        // Initialize optical depth accumulators for the secondary ray.
        float jOdRlh = 0.0;
        float jOdMie = 0.0;

        // Sample the secondary ray.
        for (int j = 0; j < jSteps; j++) {

            // Calculate the secondary ray sample position.
            vec3 jPos = iPos + pSun * (jTime + jStepSize * 0.5);

            // Calculate the height of the sample.
            float jHeight = length(jPos) - rPlanet;

            // Accumulate the optical depth.
            jOdRlh += exp(-jHeight / shRlh) * jStepSize;
            jOdMie += exp(-jHeight / shMie) * jStepSize;

            // Increment the secondary ray time.
            jTime += jStepSize;
        }

        // Calculate attenuation.
        vec3 attn = exp(-(kMie * (iOdMie + jOdMie) + kRlh * (iOdRlh + jOdRlh)));

        // Accumulate scattering.
        totalRlh += odStepRlh * attn;
        totalMie += odStepMie * attn;

        // Increment the primary ray time.
        iTime += iStepSize;

    }

    // Calculate and return the final color.
    return iSun * (pRlh * kRlh * totalRlh + pMie * kMie * totalMie);
}

void main()
{
    vec3 color = atmosphere(
        normalize(vTexCoords),          // normalized ray direction
        vec3(0,6372e3,0),               // ray origin
        uPlanetPos,                     // position of the sun
        uPlanetIntensity,               // intensity of the sun
        uPlanetRadius,                  // radius of the planet in meters
        uPlanetAtmosphereRadius,        // radius of the atmosphere in meters
        vec3(5.5e-6, 13.0e-6, 22.4e-6), // Rayleigh scattering coefficient
        21e-6,                          // Mie scattering coefficient
        8e3,                            // Rayleigh scale height
        1.2e3,                          // Mie scale height
        0.758                           // Mie preferred scattering direction
    );

    // Apply exposure.
    color = 1.0 - exp(-1.0 * color);

    FragColor = vec4(color, 1.0);
}
""");
    
    private Planet _planet = new()
    {
        Position = Vector3.One,
        Intensity = 22.0f,
        Radius = 6371e3f,
        AtmosphereRadius = 6471e3f
    };
    
    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        var view = camera.ViewMatrix;
        view.M41 = 0;
        view.M42 = 0;
        view.M43 = 0;
        
        Shader.Use();
        Shader.SetUniform("uViewMatrix", view);
        Shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);

        Shader.SetUniform("uPlanetPos", _planet.Position);
        Shader.SetUniform("uPlanetIntensity", _planet.Intensity);
        Shader.SetUniform("uPlanetRadius", _planet.Radius);
        Shader.SetUniform("uPlanetAtmosphereRadius", _planet.AtmosphereRadius);
        
        GL.DepthFunc(DepthFunction.Lequal);
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    public void DrawControls()
    {
        if (ImGui.TreeNode("Atmosphere Settings"))
        {
            ImGui.DragFloat3("Sun Direction", ref _planet.Position, 0.01f, -1.0f, 2.0f);
            ImGui.DragFloat("Sun Intensity", ref _planet.Intensity, 0.1f, 0.0f);
            ImGui.DragFloat("Sun Radius", ref _planet.Radius, 1e3f, 0.0f);
            ImGui.DragFloat("Atmosphere Radius", ref _planet.AtmosphereRadius, 1e3f, _planet.Radius + 1e3f);
            
            ImGui.TreePop();
        }
    }
    
    private struct Planet
    {
        public Vector3 Position;
        public float Intensity;
        public float Radius;
        public float AtmosphereRadius;
    }
}