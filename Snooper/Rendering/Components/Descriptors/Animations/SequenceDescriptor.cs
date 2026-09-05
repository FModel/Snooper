using System.Numerics;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine.Curves;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public sealed class SequenceDescriptor : SequenceBaseDescriptor
{
    private readonly CAnimSequence _sequence;

    public readonly int FrameCount;
    public readonly float SourceLength;
    public readonly float FrameRate;
    public readonly float RateScale;

    /// <summary>
    /// How many keys the tracks actually carry, against the <see cref="FrameCount"/> the sequence declares.
    /// They should agree, but they may not, see ACL looping mode.
    /// </summary>
    public readonly int KeyCount;
    public readonly Dictionary<string, FRichCurve>? Curves; // TODO: use with morph targets

    private readonly SegmentDescriptor[] _segments;
    public override IReadOnlyList<SegmentDescriptor> Segments => _segments;

    public SequenceDescriptor(UAnimSequence owner, AnimationDescriptor? outer = null) : base(owner, outer)
    {
        var skeleton = owner.Skeleton?.Load<USkeleton>() ?? throw new InvalidOperationException($"Failed to load skeleton for animation asset {owner.Name}");
        var converted = skeleton.ConvertAnims(owner).Sequences.FirstOrDefault() ?? throw new InvalidOperationException($"Failed to convert animation asset {owner.Name} for skeleton {skeleton.Name}");
        converted.RetargetTracks(skeleton);
        _sequence = converted;

        FrameCount = _sequence.NumFrames;
        SourceLength = _sequence.OriginalSequence.SequenceLength;
        RateScale = _sequence.OriginalSequence.RateScale;

        // the last key lands on the length rather than a frame past it, so the rate is taken over one
        // fewer. Off by that one, a sequence runs a frame fast and then holds its last key to make the
        // time back, which is a snap wherever it comes back around
        FrameRate = FrameCount > 1 && SourceLength > 0f ? (FrameCount - 1) / SourceLength : 0f;

        foreach (var track in _sequence.Tracks)
        {
            KeyCount = Math.Max(KeyCount, Math.Max(track.KeyQuat.Length, Math.Max(track.KeyPos.Length, track.KeyScale.Length)));
        }

        if (_sequence.OriginalSequence.CompressedCurveData is { FloatCurves: { Length: > 0 } curves })
        {
            Curves = new Dictionary<string, FRichCurve>(curves.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var curve in curves)
            {
                Curves[curve.CurveName.Text] = curve.FloatCurve;
            }
        }

        _segments = [new SegmentDescriptor(this)];
        Duration = _segments.Length > 0 ? _segments[0].EndPos : 0f;
    }

    public bool IsAnimatingBone(uint skeletonIndex) => _sequence.OriginalSequence.FindTrackForBoneIndex((int) skeletonIndex) >= 0;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float localTime, FTransform bindBonePose)
    {
        var boneOrientation = bindBonePose.Rotation;
        var bonePosition = bindBonePose.Translation;
        var boneScale = bindBonePose.Scale3D;

        // we are indexing into tracks but tracks are added for each skeleton bone
        _sequence.Tracks[(int) skeletonIndex].GetBoneTransform(localTime * FrameRate, FrameCount, ref boneOrientation, ref bonePosition, ref boneScale);
        return new Transform(bonePosition, boneOrientation, boneScale).ToMatrix();
    }
}
