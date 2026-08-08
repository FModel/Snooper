using CUE4Parse.UE4.Assets.Exports.Animation;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public sealed class CompositeDescriptor : CompositeBaseDescriptor
{
    public CompositeDescriptor(UAnimComposite owner) : base(owner)
    {
        AddTrack(owner.AnimationTrack, null);
    }
}
