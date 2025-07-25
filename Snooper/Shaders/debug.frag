struct PerDrawData
{
    bool IsReady;
    vec3 Color;
};

layout(std430, binding = 1) restrict readonly buffer PerDrawDataBuffer
{
    PerDrawData uDrawDataBuffer[];
};

in flat int mIndex;

out vec4 FragColor;

void main()
{
    PerDrawData drawData = uDrawDataBuffer[mIndex];
    vec3 color = vec3(0.75);
    if (drawData.IsReady)
    {
        color = drawData.Color;
    }
    
    FragColor = vec4(color, 1.0);
}