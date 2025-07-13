layout (location = 0) in vec2 aPos;

uniform float uGlobalScale;

out flat int vMatrixIndex;
out flat int vDrawID;

void main()
{
    gl_Position = vec4(aPos.x * uGlobalScale, 0.0, aPos.y * uGlobalScale, 1.0);
    
    vMatrixIndex = gl_BaseInstance + gl_InstanceID;
    vDrawID = gl_DrawID;
}