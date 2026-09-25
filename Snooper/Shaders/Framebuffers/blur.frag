in vec2 vTexCoords;

uniform sampler2D inputTexture; // ao, view depth, camera facing normal xy: what ssao.frag writes and the first pass passes through
uniform vec2 uDirection; // one input texel along the blur axis
uniform int blurRadius;

#ifdef UPSAMPLE
uniform sampler2D gPosition;
uniform sampler2D gNormal;
#endif

out vec4 FragColor;

vec3 UnpackNormal(vec2 xy)
{
    // the AO pass flips normals towards the camera, so z is positive except for a sliver at the screen edges under a
    // wide FOV, where a wrong sign only weakens a weight
    return vec3(xy, sqrt(max(1.0 - dot(xy, xy), 0.0)));
}

void main()
{
    vec4 center = texture(inputTexture, vTexCoords);
#ifdef UPSAMPLE
    vec3 centerNormal = texture(gNormal, vTexCoords).xyz;
    float centerDepth = -texture(gPosition, vTexCoords).z;
#else
    vec3 centerNormal = UnpackNormal(center.zw);
    float centerDepth = center.y;
#endif

    // Skybox/background: nothing to bilaterally weight against
    if (dot(centerNormal, centerNormal) < 0.01)
    {
#ifdef UPSAMPLE
        FragColor = vec4(1.0);
#else
        FragColor = center;
#endif
        return;
    }

    centerNormal = normalize(centerNormal);

    float result = 0.0;
    float totalWeight = 0.0;

    for (int i = -blurRadius; i <= blurRadius; i++)
    {
        vec4 tap = texture(inputTexture, vTexCoords + float(i) * uDirection);
        vec3 sampleNormal = UnpackNormal(tap.zw);
        if (dot(sampleNormal, sampleNormal) < 0.01)
            continue;

        // Reject samples across depth/normal discontinuities so the blur never bleeds
        // occlusion across silhouette edges (the source of AO "halos").
        float depthWeight = exp(-abs(tap.y - centerDepth) / max(centerDepth * 0.05, 0.05));
        float normalWeight = pow(max(dot(sampleNormal, centerNormal), 0.0), 16.0);
        float weight = depthWeight * normalWeight;

        result += tap.x * weight;
        totalWeight += weight;
    }

    float ao = totalWeight > 1e-4 ? result / totalWeight : center.x;
#ifdef UPSAMPLE
    FragColor = vec4(vec3(ao), 1.0);
#else
    FragColor = vec4(ao, center.yzw);
#endif
}
