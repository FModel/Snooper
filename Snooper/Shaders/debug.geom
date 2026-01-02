layout(lines) in;
layout(triangle_strip, max_vertices = 4) out;

#include "Buffers/PerDrawCommand.glsl"

struct PerMaterialData
{
    bool IsReady;
    float LineThickness;
    vec3 LineColor;
};

layout(std430, binding = 2) restrict readonly buffer PerMaterialDataBuffer
{
    PerMaterialData uMaterialDataBuffer[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform vec2 uViewportSize;

flat in uint vDrawID[];

flat out uint gDrawID;

void main()
{
    gDrawID = vDrawID[0];

    DrawElementsIndirectCommand cmd = uDrawCommandBuffer[gDrawID];
    PerMaterialData materialData = uMaterialDataBuffer[cmd.BaseMaterial + cmd.MaterialIndex];
    float thickness = materialData.LineThickness;

    // Get the two line endpoints in clip space
    vec4 p0 = gl_in[0].gl_Position;
    vec4 p1 = gl_in[1].gl_Position;

    // Convert to NDC
    vec2 ndc0 = p0.xy / p0.w;
    vec2 ndc1 = p1.xy / p1.w;

    // Convert to screen space
    vec2 screen0 = (ndc0 + 1.0) * 0.5 * uViewportSize;
    vec2 screen1 = (ndc1 + 1.0) * 0.5 * uViewportSize;

    // Calculate line direction and perpendicular in screen space
    vec2 dir = normalize(screen1 - screen0);
    vec2 perp = vec2(-dir.y, dir.x);

    // Calculate offset in screen space (thickness in pixels)
    vec2 offset = perp * thickness * 0.5;

    // Convert offset back to NDC space
    vec2 ndcOffset = offset / (uViewportSize * 0.5);

    // Emit quad vertices
    // Bottom-left
    gl_Position = vec4(ndc0 - ndcOffset, p0.z / p0.w, 1.0) * p0.w;
    EmitVertex();

    // Bottom-right
    gl_Position = vec4(ndc1 - ndcOffset, p1.z / p1.w, 1.0) * p1.w;
    EmitVertex();

    // Top-left
    gl_Position = vec4(ndc0 + ndcOffset, p0.z / p0.w, 1.0) * p0.w;
    EmitVertex();

    // Top-right
    gl_Position = vec4(ndc1 + ndcOffset, p1.z / p1.w, 1.0) * p1.w;
    EmitVertex();

    EndPrimitive();
}

