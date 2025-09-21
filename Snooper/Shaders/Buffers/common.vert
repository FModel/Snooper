flat out int gVertexID;
flat out int gInstanceID;
flat out int gDrawID;
flat out int gBaseVertex;
flat out int gBaseInstance;

void SetCommonVSOut()
{
    gVertexID = gl_VertexID;
    gInstanceID = gl_InstanceID;
    gDrawID = gl_DrawID;
    gBaseVertex = gl_BaseVertex;
    gBaseInstance = gl_BaseInstance;
}