struct PerInstanceData
{
    mat4 Matrix;
};

layout(std430, binding = 1) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};