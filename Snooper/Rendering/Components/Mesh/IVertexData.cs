using System.Numerics;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public interface IVertexData : TPrimitiveData<Vertex>;

public readonly struct Vertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector3 Tangent;
    public readonly Vector2 TexCoord;

    public Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord)
    {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        TexCoord = texCoord;
    }
}

public readonly struct MeshMaterialSection
{
    public readonly int MaterialIndex;
    public readonly int FirstIndex;
    public readonly int IndexCount;

    public MeshMaterialSection(int materialIndex, int firstIndex, int indexCount)
    {
        MaterialIndex = materialIndex;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }
}