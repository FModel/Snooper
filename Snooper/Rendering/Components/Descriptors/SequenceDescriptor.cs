using System.Numerics;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine.Curves;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class SequenceDescriptor
{
    private readonly CAnimSequence _sequence;

    public readonly string Name;
    public readonly string SlotName;
    public readonly float StartTime;
    public readonly float Duration;
    public readonly int FrameCount;
    public readonly Dictionary<string, FRichCurve>? Curves; // TODO: use with morph targets

    public float EndTime => StartTime + Duration;
    public float FrameRate => FrameCount / Duration;

    public SequenceDescriptor(CAnimSequence sequence)
    {
        _sequence = sequence;

        Name = _sequence.Name;
        SlotName = _sequence.SlotName ?? "DefaultSlot";
        StartTime = _sequence.StartPos;
        Duration = _sequence.AnimEndTime * _sequence.OriginalSequence.RateScale;
        FrameCount = _sequence.NumFrames;

        if (_sequence.OriginalSequence.CompressedCurveData is { FloatCurves: { Length: > 0 } curves })
        {
            Curves = new Dictionary<string, FRichCurve>(curves.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var curve in curves)
            {
                Curves[curve.CurveName.Text] = curve.FloatCurve;
            }
        }
    }

    public bool IsAnimatingBone(uint skeletonIndex) => _sequence.OriginalSequence.FindTrackForBoneIndex((int) skeletonIndex) >= 0;

    public float ToLocalTime(float time) => Duration > 0f ? (time - StartTime) * (_sequence.AnimEndTime / Duration) : 0f;
    public float FromLocalTime(float localTime) => _sequence.AnimEndTime > 0f ? StartTime + localTime * (Duration / _sequence.AnimEndTime) : StartTime;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float time, bool scale = true)
    {
        var boneOrientation = FQuat.Identity;
        var bonePosition = FVector.ZeroVector;
        var boneScale = FVector.OneVector;

        // we are indexing into tracks but tracks are added for each skeleton bone
        var frame = (time - StartTime) * FrameRate;
        _sequence.Tracks[(int) skeletonIndex].GetBoneTransform(frame, FrameCount, ref boneOrientation, ref bonePosition, ref boneScale);
        return new Transform(bonePosition, boneOrientation, scale ? boneScale : FVector.OneVector).ToMatrix();
    }
}
