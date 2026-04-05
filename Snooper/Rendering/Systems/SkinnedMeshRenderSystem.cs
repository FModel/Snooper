using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SkinnedMeshRenderSystem : MeshRenderSystem<SkinnedMeshComponent>
{
    public override uint Order => 23;
    protected override bool IsCulled => DirtyComponentsCount > 0;

    // TODO: move IndirectResources._poseData here
    // TODO: convert to a gpu driven system

    protected override void OnLoad()
    {
        foreach (var shader in Shaders.Values)
        {
            shader.Vertex = "skinned_mesh.vert";
        }

        base.OnLoad();
    }

    protected override void OnComponentUpdate(SkinnedMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component is SkeletalMeshComponent { IsPlayingAnimation: true, Descriptor.Skeleton: { } skeleton, Animation: { Sequences.Length: > 0 } animation } skeletal)
        {
            float time = ActorManager?.Time ?? delta;
            time = (time * animation.PlayRate + animation.StartTime) % skeletal.MaxAnimationDuration;

            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                // for each vertex bone, find its skeleton bone
                if (!animation.Skeleton.BoneNameToIndex.TryGetValue(boneName, out var skeletonIndex))
                    continue;

                foreach (var sequence in animation.Sequences)
                {
                    if (!sequence.IsAnimatingBone(skeletonIndex)) continue;

                    // if this sequence should be played for this frame
                    if (time >= sequence.StartTime && time < sequence.EndTime)
                    {
                        var scale = !skeleton.BoneDescriptors[boneIndex].IsRoot;
                        skeleton.BoneLocalMatrices[boneIndex] = sequence.GetBoneMatrix(skeletonIndex, time, scale);
                        break;
                    }
                }
            }

            skeleton.RecalculateBoneMatrices();
            component.MarkDirty(DirtyFlags.Animation);
        }
    }
}
