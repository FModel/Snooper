#define MESH_VERTEX
#include "Buffers/CommonMesh.vert"
#include "Buffers/common.vert"

void main()
{
    SetCommonVSOut();
    CommonMeshMain();
}