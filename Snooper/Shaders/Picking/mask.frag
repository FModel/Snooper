in vec2 vTexCoords;

uniform usampler2D pickingTexture;

layout(std430, binding = 3) readonly buffer PickedIds
{
    uint count;
    uint ids[];
} pickedIds;

out vec4 FragColor;

bool binarySearch(uint targetId, uint count)
{
    uint left = 0u;
    uint right = count - 1u;

    while (left <= right)
    {
        uint mid = (left + right) / 2u;
        uint midValue = pickedIds.ids[mid];

        if (midValue == targetId)
            return true;
        else if (midValue < targetId)
            left = mid + 1u;
        else
            right = mid - 1u;
    }

    return false;
}

void main()
{
    if (pickedIds.count == 0u)
    {
        discard; // No objects selected
    }

    uint id = texture(pickingTexture, vTexCoords).r;

    // early exit for background pixels (id == 0)
    if (id == 0u)
    {
        FragColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    bool isSelected = false;
    if (pickedIds.count <= 8u)
    {
        for (uint i = 0u; i < pickedIds.count; ++i)
        {
            if (id == pickedIds.ids[i])
            {
                isSelected = true;
                break;
            }
        }
    }
    else
    {
        isSelected = binarySearch(id, pickedIds.count);
    }

    FragColor = vec4(isSelected ? 1.0 : 0.0, 0.0, 0.0, 1.0);
}
