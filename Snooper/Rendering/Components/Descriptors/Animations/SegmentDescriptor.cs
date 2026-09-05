using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public sealed class SegmentDescriptor
{
    public readonly SequenceDescriptor Sequence;
    public readonly string SlotName; // the slot this plays on, "DefaultSlot" if none was named

    // which part of the sequence this segment reads, in sequence time
    public readonly float SourceStart; // where reading starts, 0 unless the segment was trimmed
    public readonly float SourceEnd; // where reading stops, the sequence's length unless it was trimmed
    public readonly int LoopCount; // how many times that part is replayed
    public float SourceDuration => SourceEnd - SourceStart; // how much of the sequence one pass reads
    public bool IsClipped => SourceStart > 0.0001f || SourceEnd < Sequence.SourceLength - 0.0001f;

    // where the segment sits, in timeline time
    public readonly float PlayRate; // how fast the sequence is read, so 2x takes half as long
    public readonly float StartPos; // where the segment starts
    public readonly float Duration; // how long it lasts
    public float EndPos => StartPos + Duration; // where the segment ends

    public SegmentDescriptor(SequenceDescriptor sequence, FAnimSegment? segment = null, string? slotName = null)
    {
        Sequence = sequence;
        SlotName = slotName ?? "DefaultSlot";

        SourceStart = segment?.AnimStartTime ?? 0f;
        SourceEnd = segment?.AnimEndTime ?? sequence.SourceLength;
        LoopCount = Math.Max(1, segment?.LoopingCount ?? 1);

        var rate = MathF.Abs(segment?.GetValidPlayRate() ?? sequence.RateScale);
        PlayRate = rate > 0f ? rate : 1f;
        StartPos = segment?.StartPos ?? 0f;
        Duration = LoopCount * SourceDuration / PlayRate;
    }

    public bool IsAnimatingBone(uint skeletonIndex) => Sequence.IsAnimatingBone(skeletonIndex);
    public bool IsActiveAt(float time) => time >= StartPos && time < EndPos;

    public float ToLocalTime(float time)
    {
        var local = (time - StartPos) * PlayRate;
        if (LoopCount > 1 && SourceDuration > 0f) local %= SourceDuration;

        return SourceStart + local;
    }
    public float FromLocalTime(float localTime) => PlayRate > 0f ? StartPos + (localTime - SourceStart) / PlayRate : StartPos;

    public Matrix4x4 GetBoneMatrix(uint skeletonIndex, float time, FTransform bindBonePose) => Sequence.GetBoneMatrix(skeletonIndex, ToLocalTime(time), bindBonePose);
}
