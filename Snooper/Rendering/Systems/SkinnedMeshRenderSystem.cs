using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public class SkinnedMeshRenderSystem : MeshRenderSystem<SkinnedMeshComponent>
{
    public override uint Order => 23;
    protected override bool IsCulled => _maxAnimationTime == 0.0f;

    // TODO: move IndirectResources._poseData here

    protected override void OnLoad()
    {
        foreach (var shader in Shaders.Values)
        {
            shader.Vertex = "skinned_mesh.vert";
        }

        base.OnLoad();
    }

    private float _maxAnimationTime;
    protected override void PreOnUpdate(SkinnedMeshComponent[] components)
    {
        base.PreOnUpdate(components);

        foreach (var component in components)
        {
            if (component is SkeletalMeshComponent { Animation: { } animation })
            {
                _maxAnimationTime = Math.Max(_maxAnimationTime, animation.TotalAnimTime);
            }
        }
    }

    protected override void OnComponentUpdate(SkinnedMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component is SkeletalMeshComponent { IsVisible: true, Descriptor.Skeleton: { } skeleton, Animation: { } animation })
        {
            float time = ActorManager?.Time ?? delta;
            time %= _maxAnimationTime;

            // TODO: preprocess the data and clean up the following shit

            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                // for each vertex bone, find its skeleton bone
                if (!animation.Skeleton.ReferenceSkeleton.FinalNameToIndexMap.TryGetValue(boneName, out var skeletonIndex))
                    continue;

                foreach (var sequence in animation.Sequences)
                {
                    // for this bone, find the first sequence it is animated by
                    if (sequence.OriginalSequence.FindTrackForBoneIndex(skeletonIndex) < 0)
                        continue;

                    // if this sequence should be played for this frame
                    if (time >= sequence.StartPos && time < sequence.StartPos + sequence.AnimEndTime)
                    {
                        var frame = (time - sequence.StartPos) * sequence.OriginalSequence.RateScale / (sequence.AnimEndTime / sequence.NumFrames);

                        var boneOrientation = FQuat.Identity;
                        var bonePosition = FVector.ZeroVector;
                        var boneScale = FVector.OneVector;

                        sequence.Tracks[skeletonIndex].GetBoneTransform(frame, sequence.NumFrames, ref boneOrientation, ref bonePosition, ref boneScale);

                        skeleton.BoneLocalMatrices[boneIndex] = new Transform(bonePosition, boneOrientation, boneScale).ToMatrix();
                        break;
                    }
                }
            }

            skeleton.RecalculateBoneMatrices();

            component.MarkDirty(DirtyFlags.Animation);
        }
    }
}
