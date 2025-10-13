flat out uint gVertexID;
flat out uint gInstanceID;
flat out uint gDrawID;
flat out uint gBaseVertex;
flat out uint gBaseInstance;

void SetCommonVSOut()
{
    gVertexID = gl_VertexID;
    gInstanceID = gl_InstanceID;
    gDrawID = gl_DrawID;
    gBaseVertex = gl_BaseVertex;
    gBaseInstance = gl_BaseInstance;
}