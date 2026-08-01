using System.Numerics;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine.Curves;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

/// <summary>
/// there's a lot of stuff in this class in order to support "all" kinds of UAnimationAssets
/// <para>
/// The <i>sequence</i> is the animation asset itself. It has its own keys, its own length, and its
/// own time always runs from 0 to <see cref="SourceLength"/>.
/// </para>
/// <para>
/// The <i>segment</i> is where that sequence was placed, from <see cref="StartPos"/> to <see cref="EndPos"/>.
/// A segment may read only part of its sequence, read it faster or slower, and repeat it.
/// </para>
/// <para>
/// So every time below belongs to one clock or the other. The <c>Source</c> fields are in sequence time,
/// <see cref="StartPos"/> and <see cref="Duration"/> are in timeline time, and <see cref="ToLocalTime"/>
/// is what converts between them.
/// </para>
/// </summary>
public sealed class SequenceDescriptor
{
    private readonly CAnimSequence _sequence;

    public readonly string Name;
    public readonly string SlotName; // the slot this plays on, "DefaultSlot" if none was named
    public readonly int FrameCount; // how many keys the sequence says it has
    public readonly float SourceLength; // how long the whole sequence is

    // which part of the sequence this segment reads, in sequence time
    public readonly float FrameRate; // keys per second, so (sequence time x this) is the key to sample
    public readonly float SourceStart; // where reading starts, 0 unless the segment was trimmed
    public readonly float SourceEnd; // where reading stops, SourceLength unless the segment was trimmed
    public readonly int LoopCount; // how many times that part is replayed
    public float SourceDuration => SourceEnd - SourceStart; // how much of the sequence one pass reads
    public bool IsClipped => SourceStart > 0.0001f || SourceEnd < SourceLength - 0.0001f;

    // where the segment sits, in timeline time
    public readonly float PlayRate; // how fast the sequence is read, so 2x takes half as long
    public readonly float StartPos; // where the segment starts
    public readonly float Duration; // how long it lasts
    public float EndPos => StartPos + Duration; // where the segment ends

    /// <summary>
    /// How many keys the tracks actually carry, against the <see cref="FrameCount"/> the sequence declares.
    /// They should agree, but they may not, see ACL looping mode.
    /// </summary>
    public readonly int KeyCount;
    public readonly Dictionary<string, FRichCurve>? Curves; // TODO: use with morph targets

    public SequenceDescriptor(CAnimSequence sequence, FAnimSegment? segment = null, string? slotName = null)
    {
        _sequence = sequence;
        var original = sequence.OriginalSequence;

        Name = sequence.Name;
        SlotName = slotName ?? "DefaultSlot";
        FrameCount = sequence.NumFrames;
        SourceLength = original.SequenceLength;

        // the last key lands on the length rather than a frame past it, so the rate is taken over one
        // fewer. Off by that one, a sequence runs a frame fast and then holds its last key to make the
        // time back, which is a snap wherever it comes back around
        FrameRate = FrameCount > 1 && SourceLength > 0f ? (FrameCount - 1) / SourceLength : 0f;

        SourceStart = segment?.AnimStartTime ?? 0f;
        SourceEnd = segment?.AnimEndTime ?? SourceLength;
        LoopCount = Math.Max(1, segment?.LoopingCount ?? 1);

        var rate = MathF.Abs(segment?.GetValidPlayRate() ?? original.RateScale);
        PlayRate = rate > 0f ? rate : 1f;
        StartPos = segment?.StartPos ?? 0f;
        Duration = LoopCount * SourceDuration / PlayRate;

        foreach (var track in sequence.Tracks)
        {
            KeyCount = Math.Max(KeyCount, Math.Max(track.KeyQuat.Length, Math.Max(track.KeyPos.Length, track.KeyScale.Length)));
        }

        if (original.CompressedCurveData is { FloatCurves: { Length: > 0 } curves })
        {
            Curves = new Dictionary<string, FRichCurve>(curves.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var curve in curves)
            {
                Curves[curve.CurveName.Text] = curve.FloatCurve;
            }
        }
    }

    public bool IsAnimatingBone(uint skeletonIndex) => _sequence.OriginalSequence.FindTrackForBoneIndex((int) skeletonIndex) >= 0;
    public bool IsActiveAt(float time) => time >= StartPos && time < EndPos;

    public float ToLocalTime(float time)
    {
        var local = (time - StartPos) * PlayRate;
        if (LoopCount > 1 && SourceDuration > 0f) local %= SourceDuration;

        return SourceStart + local;
    }
    public float FromLocalTime(float localTime) => PlayRate > 0f ? StartPos + (localTime - SourceStart) / PlayRate : StartPos;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float time, bool scale = true)
    {
        var boneOrientation = FQuat.Identity;
        var bonePosition = FVector.ZeroVector;
        var boneScale = FVector.OneVector;

        // we are indexing into tracks but tracks are added for each skeleton bone
        var frame = ToLocalTime(time) * FrameRate;
        _sequence.Tracks[(int) skeletonIndex].GetBoneTransform(frame, FrameCount, ref boneOrientation, ref bonePosition, ref boneScale);
        return new Transform(bonePosition, boneOrientation, scale ? boneScale : FVector.OneVector).ToMatrix();
    }
}
