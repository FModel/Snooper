using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.UObject;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class MorphDescriptor
{
    public readonly string[] Names;
    public readonly MorphLodDeltas[] Lods; // index-aligned with PrimitiveDescriptor.Lods

    public int Count => Names.Length;

    private MorphDescriptor(string[] names, MorphLodDeltas[] lods)
    {
        Names = names;
        Lods = lods;
    }

    public static MorphDescriptor? Create<TVertex>(FPackageIndex[] morphTargets, LodDescriptor<TVertex>[] lods) where TVertex : unmanaged
    {
        var loaded = new List<UMorphTarget>(morphTargets.Length);
        foreach (var ptr in morphTargets)
        {
            if (ptr.TryLoad<UMorphTarget>(out var morphTarget)) loaded.Add(morphTarget);
        }
        if (loaded.Count == 0) return null;

        var names = new string[loaded.Count];
        for (var i = 0; i < loaded.Count; i++)
        {
            names[i] = loaded[i].Name;
        }

        var lodDeltas = new MorphLodDeltas[lods.Length];
        for (var i = 0; i < lods.Length; i++)
        {
            lodDeltas[i] = Build(loaded, lods[i].SourceLodIndex, lods[i].VertexCount);
        }

        return new MorphDescriptor(names, lodDeltas);
    }

    /// <summary>
    /// Inverts every morph of a LOD: all the deltas touching a vertex end up in one contiguous run, so a
    /// single shader loop blends however many morphs affect that vertex. Two passes over the source deltas,
    /// counting then placing, so nothing intermediate is kept.
    /// </summary>
    private static MorphLodDeltas Build(List<UMorphTarget> morphTargets, uint lod, uint vertexCount)
    {
        var offsets = new uint[vertexCount + 1];
        foreach (var morphTarget in morphTargets)
        {
            var models = morphTarget.MorphLODModels;
            if (lod >= models.Length) continue;

            foreach (var vertex in models[lod].Vertices)
            {
                if (vertex.SourceIdx >= vertexCount) continue;
                offsets[vertex.SourceIdx + 1]++;
            }
        }

        for (var i = 1; i < offsets.Length; i++)
        {
            offsets[i] += offsets[i - 1];
        }

        if (offsets[vertexCount] == 0) return new MorphLodDeltas([], []);

        var deltas = new MorphDelta[offsets[vertexCount]];

        // walked forward per vertex as its run fills up, leaving offsets itself untouched
        var cursors = (uint[]) offsets.Clone();
        for (var i = 0; i < morphTargets.Count; i++)
        {
            var models = morphTargets[i].MorphLODModels;
            if (lod >= models.Length) continue;

            foreach (var vertex in models[lod].Vertices)
            {
                if (vertex.SourceIdx >= vertexCount) continue;
                deltas[cursors[vertex.SourceIdx]++] = new MorphDelta((uint) i, vertex);
            }
        }

        return new MorphLodDeltas(deltas, offsets);
    }
}

public readonly struct MorphLodDeltas(MorphDelta[] deltas, uint[] offsets)
{
    public readonly MorphDelta[] Deltas = deltas;
    public readonly uint[] Offsets = offsets;

    public bool IsEmpty => Deltas.Length == 0;
}

public readonly struct MorphDelta
{
    public readonly uint MorphIndex; // index into the mesh's morph list
    public readonly uint PositionXY;
    public readonly uint PositionZ_TangentX;
    public readonly uint TangentYZ;

    public MorphDelta(uint morphIndex, FMorphTargetDelta vertex)
    {
        var position = new Vector3(vertex.PositionDelta.X, vertex.PositionDelta.Z, vertex.PositionDelta.Y) * Settings.GlobalScale;
        var tangentZ = new Vector3(vertex.TangentZDelta.X, vertex.TangentZDelta.Z, vertex.TangentZDelta.Y);

        MorphIndex = morphIndex;
        PositionXY = Pack(position.X, position.Y);
        PositionZ_TangentX = Pack(position.Z, tangentZ.X);
        TangentYZ = Pack(tangentZ.Y, tangentZ.Z);
    }

    private static uint Pack(float low, float high) => BitConverter.HalfToUInt16Bits((Half) low) | ((uint) BitConverter.HalfToUInt16Bits((Half) high) << 16);
}
