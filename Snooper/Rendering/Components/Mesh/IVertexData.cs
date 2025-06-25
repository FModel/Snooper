using System.Numerics;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public interface IVertexData : TPrimitiveData<Vertex>;

public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Tangent;
    public Vector2 TexCoord;

    public Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        TexCoord = texCoord;
    }
}

public struct MeshMaterialSection
{
    public int MaterialIndex;
    public int FirstIndex;
    public int IndexCount;

    public MeshMaterialSection(int materialIndex, int firstIndex, int indexCount)
    {
        MaterialIndex = materialIndex;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }
}