using CUE4Parse.UE4.Assets.Exports.Animation;
using Snooper.Extensions;

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
        var descriptor = new SegmentDescriptor(Find(sequence) ?? new SequenceDescriptor(sequence, this), segment, slotName);

        Duration = MathF.Max(Duration, descriptor.EndPos);
        _segments.Add(descriptor);
    }

    private SequenceDescriptor? Find(UAnimSequence sequence)
    {
        var path = sequence.GetCleanPath() ?? "N/A";
        foreach (var segment in _segments)
        {
            if (segment.Sequence.Name == sequence.Name && segment.Sequence.Path == path)
            {
                return segment.Sequence;
            }
        }

        return null;
    }
}
