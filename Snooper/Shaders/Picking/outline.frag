in vec2 vTexCoords;

uniform sampler2D selectionMask;
uniform vec2 texelSize;       // 1.0 / screen size
uniform int outlineThickness; // e.g. 1..3 pixels

out vec4 FragColor;

void main()
{
    float mask = texture(selectionMask, vTexCoords).r;

    if (mask == 0.0)
    {
        // Not inside selection → check if near selected pixel
        bool isEdge = false;

        for (int y = -outlineThickness; y <= outlineThickness && !isEdge; ++y)
        {
            for (int x = -outlineThickness; x <= outlineThickness; ++x)
            {
                float neighbor = texture(selectionMask, vTexCoords + texelSize * vec2(x,y)).r;
                if (neighbor == 1.0)
                {
                    isEdge = true;
                    break;
                }
            }
        }

        FragColor = isEdge ? vec4(1.0) : vec4(0.0);
    }
    else
    {
        // Interior of selection: no outline
        FragColor = vec4(0.0);
    }
}
