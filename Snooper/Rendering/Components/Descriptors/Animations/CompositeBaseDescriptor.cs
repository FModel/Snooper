using CUE4Parse.UE4.Assets.Exports.Animation;
using Snooper.Rendering.Cache;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public abstract class CompositeBaseDescriptor(UAnimCompositeBase owner) : SequenceBaseDescriptor(owner)
{
    private readonly List<SegmentDescriptor> _segments = [];
    public override IReadOnlyList<SegmentDescriptor> Segments => _segments;

    protected void AddTrack(FAnimTrack track, string? slotName)
    {
        foreach (var segment in track.AnimSegments)
        {
            if (!segment.AnimReference.TryLoad<UAnimSequence>(out var sequence))
                continue;

            AddSegment(sequence, segment, slotName);
        }
    }

    private void AddSegment(UAnimSequence sequence, FAnimSegment segment, string? slotName)
    {
        var descriptor = new SegmentDescriptor(AnimationCache.GetOrCreate(sequence), segment, slotName);

        Duration = MathF.Max(Duration, descriptor.EndPos);
        _segments.Add(descriptor);
    }
}
