using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;

namespace Snooper.Rendering.Components.Descriptors;

public class MorphTargetDescriptor
{
    public readonly string Name;
    public readonly DeltaVertex[][] LodVertices;

    public MorphTargetDescriptor(UMorphTarget morphTarget)
    {
        Name = morphTarget.Name;
        LodVertices = new DeltaVertex[morphTarget.MorphLODModels.Length][];
        for (var i = 0; i < LodVertices.Length; i++)
        {
            LodVertices[i] = new DeltaVertex[morphTarget.MorphLODModels[i].Vertices.Length];
            for (var j = 0; j < LodVertices[i].Length; j++)
            {
                LodVertices[i][j] = new DeltaVertex(morphTarget.MorphLODModels[i].Vertices[j]);
            }
        }
    }
}

public readonly struct DeltaVertex(FMorphTargetDelta vertex)
{
    public readonly Vector3 Position = new(vertex.PositionDelta.X, vertex.PositionDelta.Z, vertex.PositionDelta.Y);
    public readonly Vector3 TangentZ = new(vertex.TangentZDelta.X, vertex.TangentZDelta.Z, vertex.TangentZDelta.Y);
    public readonly uint SourceIndex = vertex.SourceIdx;
}
