struct PerInstanceData
{
    mat4 Matrix;
};

layout(std430, binding = BINDING_INSTANCE_DATA) restrict readonly buffer PerInstanceDataBuffer
{
    PerInstanceData uInstanceDataBuffer[];
};