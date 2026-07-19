// Shared infinite ground plane sampling, used by both the transparent overlay grid (grid.frag)
// and the opaque shaded plane that writes into the gbuffer (grid_opaque.frag).

in VS_OUT {
    vec3 nearPoint;
    vec3 farPoint;
    vec3 cameraPosition;
} fs_in;

uniform mat4 uViewMatrix;
uniform mat4 uProjectionMatrix;
uniform float uFar;

uniform vec3 uColor;

// grid layout
uniform float uHeight;        // world height of the grid plane
uniform float uCellSize;      // world size of a single cell
uniform float uLodStep;       // cells per major division, also the lod ratio
uniform bool uAdaptive;       // rescale the grid to keep a readable density
uniform float uMinCellPixels; // smallest on-screen cell size the adaptive grid settles on

// line style
uniform float uMinorThickness; // in pixels
uniform float uMajorThickness; // in pixels
uniform float uAxisThickness;  // in pixels
uniform vec3 uMinorColor;
uniform vec3 uMajorColor;
uniform vec3 uAxisColorX;
uniform vec3 uAxisColorZ;
uniform float uMinorOpacity;
uniform float uMajorOpacity;
uniform float uOpacity;
uniform bool uShowAxes;

// distance fade, expressed as a fraction of the camera far plane
uniform float uFadeStart;
uniform float uFadeEnd;

const float kLineAA = 1.5;

/// Where the view ray through this fragment lands on the ground plane.
struct GridHit
{
    vec3 position;  // world space hit
    vec2 ddx;       // world space derivatives of the hit, for filtering
    vec2 ddy;
    float distance; // distance from the camera
    float fade;     // pattern visibility, driven by distance and view angle
    float minorSize;
    float majorSize;
    float lodFade;
};

/// Intersects the view ray with the ground plane. Returns false when it misses, the caller is
/// expected to discard: nothing of the plane is visible on that pixel.
bool TraceGrid(out GridHit hit)
{
    vec3 rayDirection = fs_in.farPoint - fs_in.nearPoint;
    if (abs(rayDirection.y) < 1e-8) return false; // the ray runs parallel to the plane

    float t = (uHeight - fs_in.nearPoint.y) / rayDirection.y;
    if (t <= 0.0) return false; // the plane is behind the camera

    hit.position = fs_in.nearPoint + t * rayDirection;
    hit.ddx = dFdx(hit.position.xz);
    hit.ddy = dFdy(hit.position.xz);

    vec3 toCamera = fs_in.cameraPosition - hit.position;
    hit.distance = length(toCamera);

    // fade out towards the far plane, and at grazing angles where a pixel covers countless cells
    hit.fade = 1.0 - smoothstep(uFadeStart * uFar, uFadeEnd * uFar, hit.distance);
    hit.fade *= smoothstep(0.0, 0.02, abs(toCamera.y) / max(hit.distance, 1e-8));

    // pick the two cell sizes bracketing the current pixel density and cross fade between them,
    // so zooming in and out never pops and never draws more than a few lines per pixel
    float lod = 0.0;
    if (uAdaptive)
    {
        float pixelSize = max(length(hit.ddx), length(hit.ddy));
        lod = max(0.0, log2(max(pixelSize * uMinCellPixels / uCellSize, 1e-8)) / log2(uLodStep));
    }

    hit.lodFade = fract(lod);
    hit.minorSize = uCellSize * pow(uLodStep, floor(lod));
    hit.majorSize = hit.minorSize * uLodStep;

    return true;
}

/// Antialiased grid coverage for a cell of `size` world units, with lines `thickness` pixels wide.
/// Adapted from Ben Golus' "pristine grid": keeps a constant pixel width and dissolves into a flat
/// tint once the cells get smaller than a pixel, instead of aliasing into moire.
float gridCoverage(vec2 position, vec2 positionDdx, vec2 positionDdy, float size, float thickness)
{
    float invSize = 1.0 / size;
    vec2 uv = position * invSize;
    vec2 ddx = positionDdx * invSize;
    vec2 ddy = positionDdy * invSize;

    vec2 derivative = max(vec2(length(vec2(ddx.x, ddy.x)), length(vec2(ddx.y, ddy.y))), vec2(1e-8));
    vec2 lineWidth = clamp(vec2(thickness) * derivative, vec2(0.0), vec2(0.5));

    vec2 drawWidth = clamp(lineWidth, derivative, vec2(0.5));
    vec2 lineAA = derivative * kLineAA;
    vec2 gridUv = abs(fract(uv) * 2.0 - 1.0);

    vec2 coverage = smoothstep(drawWidth + lineAA, drawWidth - lineAA, gridUv);
    coverage *= clamp(lineWidth / drawWidth, vec2(0.0), vec2(1.0));
    // below one cell per pixel, converge to the average coverage so the grid greys out smoothly
    coverage = mix(coverage, lineWidth, clamp(derivative * 2.0 - 1.0, vec2(0.0), vec2(1.0)));

    return mix(coverage.x, 1.0, coverage.y);
}

/// Minor line coverage, cross faded across the current lod boundary.
float MinorCoverage(GridHit hit)
{
    return mix(
        gridCoverage(hit.position.xz, hit.ddx, hit.ddy, hit.minorSize, uMinorThickness),
        gridCoverage(hit.position.xz, hit.ddx, hit.ddy, hit.majorSize, uMinorThickness), hit.lodFade);
}

/// Major line coverage, cross faded across the current lod boundary.
float MajorCoverage(GridHit hit)
{
    return mix(
        gridCoverage(hit.position.xz, hit.ddx, hit.ddy, hit.majorSize, uMajorThickness),
        gridCoverage(hit.position.xz, hit.ddx, hit.ddy, hit.majorSize * uLodStep, uMajorThickness), hit.lodFade);
}

/// Box filtered checkerboard, after Inigo Quilez. Converges to a flat 0.5 once the squares drop
/// below a pixel, which is what keeps the far half of an opaque plane from boiling.
float CheckerCoverage(vec2 position, vec2 positionDdx, vec2 positionDdy, float size)
{
    float invSize = 1.0 / size;
    vec2 uv = position * invSize;
    vec2 width = max(abs(positionDdx * invSize), abs(positionDdy * invSize)) + 1e-5;

    // integral of the square wave over the pixel footprint
    vec2 lower = (uv - 0.5 * width) * 0.5;
    vec2 upper = (uv + 0.5 * width) * 0.5;
    vec2 integral = 2.0 * (abs(fract(upper) - 0.5) - abs(fract(lower) - 0.5)) / width;

    return 0.5 - 0.5 * integral.x * integral.y;
}

/// Antialiased coverage of the line `position == 0`, `thickness` pixels wide.
float axisCoverage(float position, float derivative, float thickness)
{
    float halfWidth = max(derivative, 1e-8) * thickness * 0.5;
    return 1.0 - smoothstep(halfWidth - derivative * kLineAA, halfWidth + derivative * kLineAA, abs(position));
}

/// Blends the two world axis lines over `color`, weighted by `strength`.
vec4 ApplyAxes(vec4 color, GridHit hit, float strength)
{
    if (!uShowAxes) return color;

    float axisX = axisCoverage(hit.position.z, length(vec2(hit.ddx.y, hit.ddy.y)), uAxisThickness);
    float axisZ = axisCoverage(hit.position.x, length(vec2(hit.ddx.x, hit.ddy.x)), uAxisThickness);

    color = mix(color, vec4(uAxisColorX, 1.0), axisX * strength);
    color = mix(color, vec4(uAxisColorZ, 1.0), axisZ * strength);
    return color;
}

float ComputeDepth(vec3 position)
{
    vec4 clipPosition = uProjectionMatrix * uViewMatrix * vec4(position, 1.0);
    float ndcDepth = clipPosition.z / clipPosition.w;
    return (gl_DepthRange.diff * ndcDepth + gl_DepthRange.near + gl_DepthRange.far) * 0.5;
}
