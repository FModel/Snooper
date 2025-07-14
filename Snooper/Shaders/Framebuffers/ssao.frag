in vec2 vTexCoords;

uniform vec2 noiseScale;
uniform vec3 samples[64];
uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D noiseTexture;
uniform mat4 uProjectionMatrix;
uniform float radius;
uniform float bias;

out float FragColor;

int kernelSize = 64;

void main()
{
    vec3 fragPos = texture(gPosition, vTexCoords).xyz;
    vec3 normal = texture(gNormal, vTexCoords).xyz;
    vec3 randomVec = texture(noiseTexture, vTexCoords * noiseScale).xyz;

    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    mat3 tbn = mat3(tangent, cross(normal, tangent), normal);

    float occlusion = 0.0;
    for(int i = 0; i < kernelSize; ++i)
    {
        vec3 samplePos = tbn * samples[i];
        samplePos = fragPos + samplePos * radius;

        vec4 offset = vec4(samplePos, 1.0);
        offset = uProjectionMatrix * offset;
        offset.xyz /= offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5;

        float sampleDepth = texture(gPosition, offset.xy).z;

        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;
    }

    FragColor = 1.0 - (occlusion / kernelSize);
}