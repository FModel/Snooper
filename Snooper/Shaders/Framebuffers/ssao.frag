in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;

uniform mat4 uProjectionMatrix;

uniform float radius;       // world-space (view-space) AO radius, in engine units
uniform float uIntensity;   // power applied to the final visibility term
uniform float uMaxDistance; // view-space distance beyond which AO is fully faded out

out vec4 FragColor;

const float PI = 3.14159265359;

// Compile-time sample budget: constant trip counts let the driver fully unroll the horizon
// search instead of emitting a dynamic loop, and this is an internal quality/cost trade-off
// rather than something that needs a per-scene artist control.
const int SLICE_COUNT = 3;
const int STEPS_PER_SLICE = 4;

// Real occluded corners still receive indirect bounce light, so the darkest achievable value
// is floored instead of crushed to pure black.
const float MIN_VISIBILITY = 0.05;

// Spatial-only dithering. There's no history buffer to accumulate into here, so rotating the
// sample pattern per frame would only flicker the result rather than converge it - the
// bilateral blur pass is what cleans up this per-pixel noise instead.
float InterleavedGradientNoise(vec2 position)
{
    return fract(52.9829189 * fract(dot(position, vec2(0.06711056, 0.00583715))));
}

void main()
{
    vec3 P = texture(gPosition, vTexCoords).xyz;
    vec3 N = texture(gNormal, vTexCoords).xyz;

    float depth = -P.z;

    // Skybox/background, behind-camera, or beyond the large-world fade distance: skip the
    // horizon search entirely instead of spending samples on geometry that won't show it.
    if (dot(N, N) < 0.01 || depth <= 0.0 || depth >= uMaxDistance)
    {
        FragColor = vec4(1.0);
        return;
    }

    N = normalize(N);
    vec3 V = normalize(-P); // view vector, points from the surface towards the camera

    // Reorient towards the camera for double-sided/flipped-normal meshes (untextured
    // primitives, bad winding). Purely defensive - the occlusion math below never uses V,
    // so this can't introduce a view-angle-dependent bias, only fix backwards input data.
    if (dot(N, V) < 0.0) N = -N;

    vec2 texelSize = 1.0 / vec2(textureSize(gPosition, 0));

    // Perspective-correct screen-space radius (in pixels) for the requested world-space radius
    vec4 projected = uProjectionMatrix * vec4(radius, 0.0, P.z, 1.0);
    float radiusPixels = clamp(abs(projected.x / projected.w) * 0.5 / texelSize.x, 4.0, 256.0);

    float noise = InterleavedGradientNoise(gl_FragCoord.xy);
    float sliceRotation = noise * PI;
    float stepJitter = fract(noise * 8.423);

    float radiusSq = radius * radius;
    float occlusion = 0.0;

    for (int i = 0; i < SLICE_COUNT; i++)
    {
        float sliceAngle = sliceRotation + PI * float(i) / float(SLICE_COUNT);
        vec2 sliceDir = vec2(cos(sliceAngle), sin(sliceAngle));

        for (int side = 0; side < 2; side++)
        {
            float sideSign = side == 0 ? 1.0 : -1.0;
            float maxContribution = 0.0;

            for (int j = 0; j < STEPS_PER_SLICE; j++)
            {
                // Quadratic distribution: samples bunch up close to the pixel, where contact
                // occlusion matters most, and spread out further away.
                float t = (float(j) + stepJitter) / float(STEPS_PER_SLICE);
                float stepDist = t * t * radiusPixels;

                // gPosition is clamped-to-edge, so marching past the screen bounds just
                // resamples the edge pixel instead of needing a manual UV bounds check.
                vec2 sampleUV = vTexCoords + sliceDir * sideSign * stepDist * texelSize;
                vec3 sampleP = texture(gPosition, sampleUV).xyz;

                vec3 horizonVec = sampleP - P;
                float horizonDistSq = dot(horizonVec, horizonVec);
                if (horizonDistSq < 1e-8)
                    continue;

                float horizonDist = sqrt(horizonDistSq);
                vec3 horizonDir = horizonVec / horizonDist;

                // Direct cosine-weighted contribution against the real surface normal - no
                // signed reference angle is synthesized here, so there's nothing for a
                // view-angle-dependent sign error to corrupt; this stays correct at any
                // viewing angle by construction, since V never enters this computation.
                float contribution = max(0.0, dot(horizonDir, N));

                // Smoothly fade the contribution out near the radius boundary instead of a
                // hard cutoff, which is what causes a visible ring/halo artifact.
                float falloff = clamp(1.0 - horizonDistSq / radiusSq, 0.0, 1.0);
                contribution *= falloff;

                // Track the single strongest occluder along the ray (a proper horizon search)
                // instead of summing every sample, which would double-count stacked geometry.
                maxContribution = max(maxContribution, contribution);
            }

            occlusion += maxContribution;
        }
    }

    occlusion /= float(SLICE_COUNT * 2);

    float visibility = clamp(1.0 - occlusion, 0.0, 1.0);
    float ao = mix(MIN_VISIBILITY, 1.0, pow(visibility, uIntensity));
    ao = mix(1.0, ao, 1.0 - smoothstep(uMaxDistance * 0.7, uMaxDistance, depth));

    FragColor = vec4(vec3(ao), 1.0);
}
