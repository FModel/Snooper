using System.Numerics;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Mesh;

public interface IVertexData : TPrimitiveData<Vertex>;

public readonly struct Vertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texCoord)
{
    public readonly Vector3 Position = position;
    public readonly Vector3 Normal = normal;
    public readonly Vector3 Tangent = tangent;
    public readonly Vector2 TexCoord = texCoord;
}