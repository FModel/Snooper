using System.Numerics;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Material;
using Serilog;
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

public class MeshMaterialSection(int materialIndex, int firstIndex, int indexCount)
{
    public readonly int FirstIndex = firstIndex;
    public readonly int IndexCount = indexCount;
    public readonly CMaterialParams2 Parameters = new();

    public void ParseMaterialAsync(ResolvedObject?[] materials, Action? onParsed = null)
    {
        Task.Run(() =>
        {
            if (materialIndex >= 0 && materialIndex < materials.Length)
            {
                if (materials[materialIndex]?.TryLoad(out var m) == true && m is UMaterialInterface material)
                {
                    material.GetParams(Parameters, EMaterialFormat.FirstLayer);
                }
                else
                {
                    Log.Warning("Material at index {MatIndex} is not valid or could not be loaded.", materialIndex);
                }
            }
            else
            {
                Log.Warning("Material index {MatIndex} is out of bounds for mesh component with {MaterialsLength} materials.", materialIndex, materials.Length);
            }
        }).ContinueWith(_ =>
        {
            onParsed?.Invoke();
        }, TaskScheduler.Default);
    }
}