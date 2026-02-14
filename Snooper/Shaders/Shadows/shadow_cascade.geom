layout(triangles, invocations = 4) in;
layout(triangle_strip, max_vertices = 3) out;

uniform mat4 uViewMatrices[4];
uniform mat4 uProjectionMatrices[4];

void main()
{
    mat4 viewProjection = uProjectionMatrices[gl_InvocationID] * uViewMatrices[gl_InvocationID];

    for (int i = 0; i < 3; i++)
    {
        gl_Position = viewProjection * gl_in[i].gl_Position;
        gl_Layer = gl_InvocationID;

        EmitVertex();
    }

    EndPrimitive();
}
