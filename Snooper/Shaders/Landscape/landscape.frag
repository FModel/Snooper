layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gColor;
layout (location = 3) out vec4 gSpecular;

in TE_OUT {
    vec3 vViewPos;
    mat3 TBN;
    vec3 vColor;
} fs_in;

void main()
{
    gPosition = fs_in.vViewPos;
    gNormal = normalize(fs_in.TBN * vec3(0.0, 0.0, 1.0));
    gColor.rgb = fs_in.vColor;
    gColor.a = 1.0; // free space
    gSpecular.rgb = vec3(0.0, 0.0, 0.0);
    gSpecular.a = 1.0; // free space
}