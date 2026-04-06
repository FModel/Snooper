using System.Numerics;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class SequenceDescriptor(CAnimSequence sequence)
{
    public readonly string Name = sequence.Name;
    public readonly float StartTime = sequence.StartPos;
    public readonly float Duration = sequence.AnimEndTime * sequence.OriginalSequence.RateScale;
    public readonly int FrameCount = sequence.NumFrames;

    public float EndTime => StartTime + Duration;
    public float FrameRate => FrameCount / Duration;

    public bool IsAnimatingBone(uint skeletonIndex) => sequence.OriginalSequence.FindTrackForBoneIndex((int) skeletonIndex) >= 0;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float time, bool scale = true)
    {
        var boneOrientation = FQuat.Identity;
        var bonePosition = FVector.ZeroVector;
        var boneScale = FVector.OneVector;

        // we are indexing into tracks but tracks are added for each skeleton bone
        var frame = (time - StartTime) * FrameRate;
        sequence.Tracks[(int) skeletonIndex].GetBoneTransform(frame, FrameCount, ref boneOrientation, ref bonePosition, ref boneScale);
        return new Transform(bonePosition, boneOrientation, scale ? boneScale : FVector.OneVector).ToMatrix();
    }
}
