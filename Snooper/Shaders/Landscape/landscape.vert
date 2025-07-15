layout (location = 0) in vec2 aPos;

uniform float uGlobalScale;

out flat int vInstanceIndex;
out flat int vDrawIndex;

void main()
{
    gl_Position = vec4(aPos.x * uGlobalScale, 0.0, aPos.y * uGlobalScale, 1.0);

    vInstanceIndex = gl_BaseInstance + gl_InstanceID;
    vDrawIndex = gl_DrawID;
}