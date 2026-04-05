using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class AnimationDescriptor
{
    public string Name { get; }
    public string Path { get; }

    public readonly SkeletonDescriptor Skeleton;
    public readonly SequenceDescriptor[] Sequences;
    public readonly float Duration;

    public readonly float StartTime;
    public readonly float PlayRate;

    public AnimationDescriptor(UAnimationAsset animToPlay, float startTime = 0f, float playRate = 1f)
    {
        Name = animToPlay.Name;
        Path = animToPlay.Owner?.Provider?.FixPath(animToPlay.GetPathName()) ?? "N/A";

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

        StartTime = startTime;
        PlayRate = playRate;
    }
}
