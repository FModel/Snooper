layout (location = 1) out uint gPicking;

#include "Skybox/sky.glsl"
#include "Lighting/fog.glsl"

in vec3 vTexCoords;

uniform bool useSun; // without one nothing lights the atmosphere, only the fog shows
uniform sampler2D uSky;
uniform vec3 uSunPos;
uniform float uSunDisc; // cosine of the sun's angular radius

out vec4 FragColor;

void main()
{
    vec3 direction = normalize(vTexCoords);
    vec3 color = vec3(0.0);
    if (useSun)
    {
        color = texture(uSky, SkyDirectionToUv(direction)).rgb;

        // the sun itself, too small for the texture: a disc as wide as its light says, tinted by the sky around it so it reddens with it
        float mu = dot(direction, normalize(uSunPos));
        float edge = max(fwidth(mu), 1e-7);
        float disc = smoothstep(uSunDisc - edge, uSunDisc + edge, mu);
        vec3 tint = color / max(max(color.r, max(color.g, color.b)), 1e-4);
        color += disc * 50.0 * tint * smoothstep(-0.01, 0.01, direction.y);
    }

    FragColor = vec4(ApplySkyFog(SkyExposure(color), direction), 1.0);
    
    gPicking = 0u;
}