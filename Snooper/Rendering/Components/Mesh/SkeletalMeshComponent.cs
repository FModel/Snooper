using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class SkeletalMeshComponent : SkinnedMeshComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Animation;

    public AnimationDescriptor? Animation { get; private set; }

    public float MaxAnimationDuration
    {
        get
        {
            if (Relation is SkeletalMeshComponent skeletal)
            {
                return skeletal.MaxAnimationDuration;
            }
            return Animation?.Duration ?? 0.0f;
        }
    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh, transform)
    {

    }

    public SkeletalMeshComponent(USkeleton skeleton, Transform? transform = null) : base(skeleton, transform)
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(skeletalMesh, component)
    {
        if (component.AnimationData is { } animationData && animationData.AnimToPlay.TryLoad<UAnimationAsset>(out var animToPlay))
        {
            SetAnimation(animToPlay, animationData.SavedPosition, animationData.SavedPlayRate);
        }
    }

    public void SetAnimation(UAnimationAsset animToPlay, float startTime = 0f, float playRate = 1f)
    {
        Animation = new AnimationDescriptor(animToPlay, startTime, playRate);
    }
}
