using System.Numerics;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class SequenceDescriptor(CAnimSequence sequence)
{
    public readonly float StartTime = sequence.StartPos;
    public readonly float Duration = sequence.AnimEndTime;
    public readonly float RateScale = sequence.OriginalSequence.RateScale;
    public readonly int FrameCount = sequence.NumFrames;

    public float EndTime => StartTime + Duration;
    public float FrameRate => FrameCount / Duration * RateScale;

    public bool IsAnimatingBone(uint skeletonIndex) => sequence.OriginalSequence.FindTrackForBoneIndex((int) skeletonIndex) >= 0;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float time)
    {
        var boneOrientation = FQuat.Identity;
        var bonePosition = FVector.ZeroVector;
        var boneScale = FVector.OneVector;

        // we are indexing into tracks but tracks are added for each skeleton bone
        var frame = (time - StartTime) * FrameRate;
        sequence.Tracks[(int) skeletonIndex].GetBoneTransform(frame, FrameCount, ref boneOrientation, ref bonePosition, ref boneScale);
        return new Transform(bonePosition, boneOrientation, boneScale).ToMatrix();
    }
}
