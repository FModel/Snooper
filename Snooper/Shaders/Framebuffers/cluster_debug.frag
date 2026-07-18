in vec2 vTexCoords;

#include "Buffers/PerLightData.glsl"

uniform sampler2D gPosition;
uniform sampler2D sceneTexture;

uniform int uGridDimX;
uniform int uGridDimY;
uniform int uGridDimZ;
uniform float uZNear;
uniform float uZFar;

uniform int uMode;              // 0 = lights in this cluster, 1 = cluster Z slice, 2 = busiest cluster of the column
uniform int uMaxLightsPerCluster;
uniform float uOverlay;         // 0 = scene only, 1 = visualization only
uniform bool uShowGrid;
uniform bool uHasLights;        // false => cluster buffers are not bound, don't touch them

out vec4 FragColor;

// Must match TileSize in ClusteredLightSystem / cluster_build.comp.
const float TILE_SIZE = 32.0;

const float LEGEND_WIDTH = 0.28;
const float LEGEND_HEIGHT = 0.022;
const float LEGEND_MARGIN = 0.02;

// Blue -> cyan -> green -> yellow -> red. Perceptually ordered enough to read density at a glance.
vec3 Heatmap(float t)
{
    t = clamp(t, 0.0, 1.0);
    if (t < 0.25) return mix(vec3(0.0, 0.0, 0.5), vec3(0.0, 0.8, 1.0), t / 0.25);
    if (t < 0.50) return mix(vec3(0.0, 0.8, 1.0), vec3(0.0, 1.0, 0.2), (t - 0.25) / 0.25);
    if (t < 0.75) return mix(vec3(0.0, 1.0, 0.2), vec3(1.0, 1.0, 0.0), (t - 0.50) / 0.25);
    return mix(vec3(1.0, 1.0, 0.0), vec3(1.0, 0.1, 0.0), (t - 0.75) / 0.25);
}

uvec2 GetClusterXY()
{
    return uvec2(
        min(uint(gl_FragCoord.x / TILE_SIZE), uint(uGridDimX - 1)),
        min(uint(gl_FragCoord.y / TILE_SIZE), uint(uGridDimY - 1))
    );
}

// Same exponential slicing as light_clustered.frag / cluster_build.comp.
uint GetClusterZ(float viewZ)
{
    float depth = clamp(-viewZ, uZNear, uZFar);
    float slice = log(depth / uZNear) / log(uZFar / uZNear) * float(uGridDimZ);
    return uint(clamp(slice, 0.0, float(uGridDimZ - 1)));
}

uint GetClusterIndex(uvec2 xy, uint z)
{
    return z * uint(uGridDimX) * uint(uGridDimY) + xy.y * uint(uGridDimX) + xy.x;
}

void main()
{
    vec3 viewPos = texture(gPosition, vTexCoords).rgb;
    vec3 scene = texture(sceneTexture, vTexCoords).rgb;

    // The G-buffer stores nothing for background pixels, so there is no cluster to report there.
    bool isBackground = dot(viewPos, viewPos) < 1e-8;

    uvec2 clusterXY = GetClusterXY();
    vec3 vizColor = vec3(0.0);
    bool hasViz = false;

    if (!uHasLights)
    {
        FragColor = vec4(scene, 1.0);
        return;
    }

    if (uMode == 2)
    {
        // Depth-independent view: the fullest cluster anywhere along this tile's column. Shows where
        // lights pile up even when the visible surface sits in an empty slice (or there is none).
        uint maxCount = 0u;
        for (uint z = 0u; z < uint(uGridDimZ); z++)
            maxCount = max(maxCount, clusterData[GetClusterIndex(clusterXY, z)].count);

        vizColor = Heatmap(float(maxCount) / float(uMaxLightsPerCluster));
        hasViz = maxCount > 0u;
    }
    else if (!isBackground)
    {
        uint clusterZ = GetClusterZ(viewPos.z);
        uint clusterIndex = GetClusterIndex(clusterXY, clusterZ);

        if (uMode == 1)
        {
            // Cycling hue per slice makes the exponential Z distribution visible as banding in depth.
            vizColor = Heatmap(float(clusterZ) / float(max(uGridDimZ - 1, 1)));
            hasViz = true;
        }
        else
        {
            uint count = clusterData[clusterIndex].count;
            vizColor = Heatmap(float(count) / float(uMaxLightsPerCluster));

            // A cluster at the cap is silently dropping lights -- flag it instead of shading it red.
            if (count >= uint(uMaxLightsPerCluster))
                vizColor = vec3(1.0, 0.0, 1.0);

            hasViz = count > 0u;
        }
    }

    // Scene stays underneath, desaturated, so geometry reads as context without competing with the ramp.
    float luma = dot(scene, vec3(0.299, 0.587, 0.114));
    vec3 color = mix(scene, vec3(luma) * 0.6, uOverlay);
    if (hasViz) color = mix(color, vizColor, uOverlay);

    if (uShowGrid)
    {
        // gl_FragCoord is pixel-centered (x.5), so the closest a fragment ever gets to a tile edge is
        // exactly 0.5 -- the test has to include that distance or no fragment ever qualifies.
        vec2 tileFraction = fract(gl_FragCoord.xy / TILE_SIZE);
        vec2 edge = min(tileFraction, 1.0 - tileFraction) * TILE_SIZE;
        if (min(edge.x, edge.y) <= 0.5)
            color = mix(color, vec3(1.0), 0.4 * uOverlay);
    }

    // Legend: the ramp itself, bottom-left, running 0 -> uMaxLightsPerCluster.
    if (uMode != 1)
    {
        vec2 legendMin = vec2(LEGEND_MARGIN);
        vec2 legendMax = legendMin + vec2(LEGEND_WIDTH, LEGEND_HEIGHT);
        if (all(greaterThanEqual(vTexCoords, legendMin)) && all(lessThanEqual(vTexCoords, legendMax)))
        {
            float t = (vTexCoords.x - legendMin.x) / LEGEND_WIDTH;
            vec2 border = min(vTexCoords - legendMin, legendMax - vTexCoords);
            color = min(border.x, border.y) < 0.002 ? vec3(1.0) : Heatmap(t);
        }
    }

    FragColor = vec4(color, 1.0);
}
