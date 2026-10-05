#include "Skybox/atmosphere.glsl"
#include "Skybox/sky.glsl"

in vec2 vTexCoords;

uniform vec3 uSunPos;
uniform vec3 uSunLuminance;

uniform float uPlanetRadius;
uniform float uAtmosphereHeight;
uniform float uAltitude;
uniform vec3 uRayleighScattering;
uniform float uRayleighScaleHeight;
uniform float uMieScattering;
uniform float uMieAbsorption;
uniform float uMieScaleHeight;
uniform float uMieAnisotropy;
uniform vec3 uOzoneAbsorption;

out vec4 FragColor;

void main()
{
    vec3 color = atmosphere(
        SkyUvToDirection(vTexCoords),           // normalized ray direction
        vec3(0, uPlanetRadius + uAltitude, 0),  // ray origin
        uSunPos,                                // position of the sun
        1.0,                                    // intensity of the sun, applied below with its colour
        uPlanetRadius,                          // radius of the planet in meters
        uPlanetRadius + uAtmosphereHeight,      // radius of the atmosphere in meters
        uRayleighScattering,                    // Rayleigh scattering coefficient
        uMieScattering,                         // Mie scattering coefficient
        uRayleighScaleHeight,                   // Rayleigh scale height
        uMieScaleHeight,                        // Mie scale height
        uMieAnisotropy,                         // Mie preferred scattering direction
        uMieAbsorption,                         // Mie absorption coefficient
        uOzoneAbsorption                        // ozone absorption coefficient
    );

    FragColor = vec4(color * uSunLuminance, 1.0);
}
