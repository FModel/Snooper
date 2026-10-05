#define SKY_PI 3.141592653589793

vec2 SkyDirectionToUv(vec3 direction)
{
    float azimuth = atan(direction.z, direction.x);
    float elevation = asin(clamp(direction.y, -1.0, 1.0));
    float row = sign(elevation) * sqrt(abs(elevation) / (0.5 * SKY_PI));
    return vec2(azimuth / (2.0 * SKY_PI) + 0.5, row * 0.5 + 0.5);
}

vec3 SkyUvToDirection(vec2 uv)
{
    float azimuth = (uv.x - 0.5) * 2.0 * SKY_PI;
    float row = uv.y * 2.0 - 1.0;
    float elevation = sign(row) * row * row * 0.5 * SKY_PI;
    float c = cos(elevation);
    return vec3(c * cos(azimuth), sin(elevation), c * sin(azimuth));
}

// the texture holds the scattered light, this is what goes on screen
vec3 SkyExposure(vec3 color)
{
    return 1.0 - exp(-color);
}
