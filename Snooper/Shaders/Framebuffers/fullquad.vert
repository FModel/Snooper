out vec2 vTexCoords;

void main()
{
    // one triangle that covers the screen, from the vertex index alone
    vec2 position = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2) * 2.0 - 1.0;

    gl_Position = vec4(position, 0.0, 1.0);
    vTexCoords = position * 0.5 + 0.5;
}
