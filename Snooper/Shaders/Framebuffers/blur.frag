in vec2 vTexCoords;

uniform sampler2D inputTexture;
uniform sampler2D gPosition;
uniform sampler2D gNormal;

uniform vec2 texelSize;
uniform int blurRadius;

out vec4 FragColor;

void main()
{
    vec3 centerPos = texture(gPosition, vTexCoords).xyz;
    vec3 centerNormal = texture(gNormal, vTexCoords).xyz;

    // Skybox/background: nothing to bilaterally weight against, pass through untouched
    if (dot(centerNormal, centerNormal) < 0.01)
    {
        FragColor = texture(inputTexture, vTexCoords);
        return;
    }

    centerNormal = normalize(centerNormal);
    float centerDepth = -centerPos.z;

    float result = 0.0;
    float totalWeight = 0.0;

    for (int x = -blurRadius; x <= blurRadius; x++)
    {
        for (int y = -blurRadius; y <= blurRadius; y++)
        {
            vec2 sampleUV = vTexCoords + vec2(float(x), float(y)) * texelSize;

            vec3 sampleNormal = texture(gNormal, sampleUV).xyz;
            if (dot(sampleNormal, sampleNormal) < 0.01)
                continue;

            vec3 samplePos = texture(gPosition, sampleUV).xyz;
            float sampleDepth = -samplePos.z;

            // Reject samples across depth/normal discontinuities so the blur never bleeds
            // occlusion across silhouette edges (the source of AO "halos").
            float depthWeight = exp(-abs(sampleDepth - centerDepth) / max(centerDepth * 0.05, 0.05));
            float normalWeight = pow(max(dot(sampleNormal, centerNormal), 0.0), 16.0);
            float weight = depthWeight * normalWeight;

            result += texture(inputTexture, sampleUV).r * weight;
            totalWeight += weight;
        }
    }

    float ao = totalWeight > 1e-4 ? result / totalWeight : texture(inputTexture, vTexCoords).r;
    FragColor = vec4(vec3(ao), 1.0);
}
