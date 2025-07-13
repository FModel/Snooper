in VS_OUT {
    vec3 vViewPos;
    vec2 vTexCoords;
    mat3 TBN;
    vec3 vDebugColor;
} fs_in;

out vec4 FragColor;

void main()
{
    vec3 tangent = normalize(fs_in.TBN * vec3(1.0, 0.0, 0.0));
    vec3 bitangent = normalize(fs_in.TBN * vec3(0.0, 1.0, 0.0));
    vec3 normal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));

    float tFactor = 0.9 + 0.1 * tangent.z;
    float bFactor = 0.9 + 0.1 * bitangent.z;
    float nFactor = 0.7 + 0.3 * normal.z;

    float brightness = (tFactor + bFactor + nFactor) / 3.0;
    FragColor = vec4(fs_in.vDebugColor * brightness, 1.0);
}