using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public class SkinnedMeshRenderSystem : MeshRenderSystem<SkinnedMeshComponent>
{
    public override uint Order => 23;

    // TODO: move IndirectResources._poseData here

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

        if (component is SkeletalMeshComponent { IsVisible: true, Descriptor.Skeleton: { } skeleton,  Animation: { } animation })
        {
            float time = ActorManager?.Time ?? delta;
            time %= animation.TotalAnimTime;

            var sequenceIndex = 0;
            for (var i = 0; i < animation.Sequences.Count; i++)
            {
                var s = animation.Sequences[i];
                if (time >= s.StartPos && time < s.StartPos + s.AnimEndTime)
                {
                    sequenceIndex = i;
                    break;
                }
            }

            var sequence = animation.Sequences[sequenceIndex];
            var frame = (time - sequence.StartPos) * sequence.FramesPerSecond;

            foreach (var (boneName, boneIndex) in skeleton.BoneNameToIndex)
            {
                if (!animation.Skeleton.ReferenceSkeleton.FinalNameToIndexMap.TryGetValue(boneName, out var trackIndex))
                    continue;

                var boneOrientation = FQuat.Identity;
                var bonePosition = FVector.ZeroVector;
                var boneScale = FVector.OneVector;

                sequence.Tracks[trackIndex].GetBoneTransform(frame, sequence.NumFrames, ref boneOrientation, ref bonePosition, ref boneScale);

                skeleton.BoneLocalMatrices[boneIndex] = new Transform(bonePosition, boneOrientation, boneScale).ToMatrix();
            }

            skeleton.RecalculateBoneMatrices();
            component.MarkDirty(DirtyFlags.Animation);
        }
    }
}
