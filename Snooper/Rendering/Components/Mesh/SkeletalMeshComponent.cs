using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Core;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(AnimationSystem))]
public class SkeletalMeshComponent : SkinnedMeshComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Animation;

    public CAnimSet? Animation { get; }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, Transform? transform = null) : base(skeletalMesh, transform)
    {

    }

    public SkeletalMeshComponent(USkeletalMesh skeletalMesh, USkeletalMeshComponent component) : base(skeletalMesh, component)
    {
        if (component.AnimationData is { } animationData && animationData.AnimToPlay.TryLoad<UAnimationAsset>(out var animToPlay))
        {
            Animation = animToPlay.ConvertAnims();
            foreach (var sequence in Animation.Sequences)
            {
                sequence.RetargetTracks(Animation.Skeleton);
            }
        }
    }

    public override void Update(IndirectResources<Vertex, PerInstanceData, PerMaterialMeshData> resources)
    {
        base.Update(resources);

        if (Animation != null)
        {
            MarkDirty(DirtyFlags.Animation);
        }
    }
}
