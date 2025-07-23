in vec2 vTexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D noiseTexture;

uniform vec3 samples[64];
uniform vec2 noiseScale;

uniform float radius;
uniform mat4 uProjectionMatrix;

out float FragColor;

const int kernelSize = 64;

void main()
{
    vec3 fragPos = texture(gPosition, vTexCoords).xyz;
    vec3 normal = normalize(texture(gNormal, vTexCoords).xyz);
    vec3 randomVec = normalize(texture(noiseTexture, vTexCoords * noiseScale).xyz);

    // Construct TBN matrix from normal and randomVec
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    float occlusion = 0.0;
    float depth = abs(fragPos.z);
    float adaptiveRadius = radius * clamp(depth / 10.0, 0.1, 3.0);
    float adaptiveBias = mix(0.005, 0.05, clamp(depth / 30.0, 0.0, 1.0));

    for (int i = 0; i < kernelSize; ++i)
    {
        // Transform sample to view space
        vec3 sampleVec = TBN * samples[i];
        vec3 samplePos = fragPos + sampleVec * adaptiveRadius;

        // Project sample position into screen space
        vec4 offset = vec4(samplePos, 1.0);
        offset = uProjectionMatrix * offset;
        offset.xyz /= offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5; // NDC to screen space

        if (offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0)
        {
            continue;
        }

        float sampleDepth = texture(gPosition, offset.xy).z;
        float rangeCheck = smoothstep(0.0, 1.0, adaptiveRadius / abs(fragPos.z - sampleDepth + adaptiveBias));

        float depthDiff = sampleDepth - samplePos.z;
        float visibility = depthDiff > adaptiveBias ? 1.0 : 0.0;

        // Distance falloff
        float distance = length(samplePos - fragPos);
        float falloff = smoothstep(0.0, adaptiveRadius, distance);

        occlusion += visibility * rangeCheck * falloff;
    }

    // Normalize and apply curve
    occlusion = occlusion / float(kernelSize);
    occlusion = pow(clamp(1.0 - occlusion, 0.0, 1.0), 1.5); // control darkness with exponent

    FragColor = occlusion;
}