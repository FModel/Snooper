using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public class AnimationSystem : ActorSystem<SkeletalMeshComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Animation;
    public override uint Order => 98;

    protected override void OnRender(CameraComponent camera, CommandBufferType type)
    {

    }

    protected override void OnComponentUpdate(SkeletalMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);
        if (!component.IsDirty(DirtyFlags.Animation))
            return;

        if (component is { Animation: { } animation, Descriptor.Skeleton: { } skeleton })
        {
            var sequence = animation.Sequences[0];
            float time = ActorManager?.Time ?? delta;
            float frame = time * sequence.FramesPerSecond % sequence.NumFrames;

            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                if (!animation.Skeleton.ReferenceSkeleton.FinalNameToIndexMap.TryGetValue(boneName, out var skeletonIndex))
                    continue;

                var boneOrientation = FQuat.Identity;
                var bonePosition = FVector.ZeroVector;
                var boneScale = FVector.OneVector;

                sequence.Tracks[skeletonIndex].GetBoneTransform(frame, sequence.NumFrames, ref boneOrientation, ref bonePosition, ref boneScale);

                skeleton.BoneLocalMatrices[boneIndex] = new Transform(bonePosition, boneOrientation, boneScale).ToMatrix();
            }
            skeleton.RecalculateBoneMatrices();
        }
        // component.MarkClean(DirtyFlags.Animation);
    }
}
