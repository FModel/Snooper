in vec2 vTexCoords;

uniform sampler2DArray shadowTexture;
uniform sampler2D cameraTexture;
uniform int cascadeCount;
uniform int gridCols;
uniform int gridRows;
uniform vec2 cellSize;

out vec4 FragColor;

const float borderThickness = 0.001;

void main()
{
    vec2 cameraSize = vec2(0.25);
    vec2 cameraCenter = vec2(0.5);
    vec2 cameraMin = cameraCenter - cameraSize * 0.5;
    vec2 cameraMax = cameraCenter + cameraSize * 0.5;

    if (vTexCoords.x >= cameraMin.x && vTexCoords.x <= cameraMax.x &&
        vTexCoords.y >= cameraMin.y && vTexCoords.y <= cameraMax.y)
    {
        vec2 cameraUV = (vTexCoords - cameraMin) / cameraSize;
        FragColor = texture(cameraTexture, cameraUV);
        return;
    }

    int colIndex = int(vTexCoords.x / cellSize.x);
    int rowIndex = int(vTexCoords.y / cellSize.y);

    int cascadeIndex = rowIndex * gridCols + colIndex;
    if (cascadeIndex >= cascadeCount)
    {
        FragColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    vec2 cellPos = vec2(
        vTexCoords.x - float(colIndex) * cellSize.x,
        vTexCoords.y - float(rowIndex) * cellSize.y
    );

    float borderX = min(cellPos.x, cellSize.x - cellPos.x);
    float borderY = min(cellPos.y, cellSize.y - cellPos.y);

    bool isOnBorder = borderX < borderThickness || borderY < borderThickness;
    if (isOnBorder)
    {
        FragColor = vec4(1.0, 1.0, 1.0, 1.0);
        return;
    }

    vec3 cascadeColors[4] = vec3[](
        vec3(1.0, 0.0, 0.0), // Red
        vec3(0.0, 1.0, 0.0), // Green
        vec3(0.0, 0.0, 1.0), // Blue
        vec3(1.0, 1.0, 0.0)  // Yellow
    );

    vec3 cascadeColor = cascadeColors[cascadeIndex % 4];
    float depth = texture(shadowTexture, vec3(cellPos / cellSize, float(cascadeIndex))).r;

    vec3 finalColor = mix(vec3(depth), cascadeColor, 0.5);
    FragColor = vec4(finalColor, 1.0);
}
