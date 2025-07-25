layout (location = 0) in vec3 aPos;

struct PerInstanceData
{
    mat4 Matrix;
};

layout(std430, binding = 0) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;

out flat int mIndex;

void main()
{
    mIndex = gl_DrawID;
    gl_Position = uProjectionMatrix * uViewMatrix * uInstanceDataBuffer[gl_BaseInstance + gl_InstanceID].Matrix * vec4(aPos, 1.0);
}