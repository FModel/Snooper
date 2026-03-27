using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class AnimationDescriptor
{
    public readonly SkeletonDescriptor Skeleton;
    public readonly SequenceDescriptor[] Sequences;
    public readonly float Duration;

    public AnimationDescriptor(UAnimationAsset animToPlay)
    {
        var animation = animToPlay.ConvertAnims();

        Skeleton = new SkeletonDescriptor(animation.Skeleton.ReferenceSkeleton);

        Sequences = new SequenceDescriptor[animation.Sequences.Count];
        for (var i = 0; i < Sequences.Length; i++)
        {
            var sequence = animation.Sequences[i];
            sequence.RetargetTracks(animation.Skeleton);
            Sequences[i] = new SequenceDescriptor(sequence);
        }

        if (Sequences.Length > 0)
            Duration = Sequences[^1].EndTime;
    }
}
