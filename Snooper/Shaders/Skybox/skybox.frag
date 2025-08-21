#include "Skybox/atmosphere.glsl"

in vec3 vTexCoords;

uniform vec3 uSunPos;
uniform float uSunIntensity;
uniform float uSunRadius;
uniform float uSunAtmosphereRadius;

out vec4 FragColor;

void main()
{
    vec3 color = atmosphere(
        normalize(vTexCoords),          // normalized ray direction
        vec3(0, 6372e3, 0),             // ray origin
        uSunPos,                        // position of the sun
        uSunIntensity,                  // intensity of the sun
        uSunRadius,                     // radius of the planet in meters
        uSunAtmosphereRadius,           // radius of the atmosphere in meters
        vec3(5.5e-6, 13.0e-6, 22.4e-6), // Rayleigh scattering coefficient
        21e-6,                          // Mie scattering coefficient
        8e3,                            // Rayleigh scale height
        1.2e3,                          // Mie scale height
        0.758                           // Mie preferred scattering direction
    );

    // Apply exposure.
    color = 1.0 - exp(-1.0 * color);

    FragColor = vec4(color, 1.0);
}