using CUE4Parse.UE4.Objects.Core.Math;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Systems;

public class AnimationSystem : ActorSystem<SkeletalMeshComponent>
{
    public override ActorSystemType SystemType => ActorSystemType.Animation;
    public override uint Order => 98;

    protected override void OnRender(CameraComponent camera, CommandBufferType type)
    {
        // TODO: there's a nasty bug with animations when there's more static meshes than skeletal meshes????
        // world space bone matrices are correct but skinned mesh in the shader does not follow the bones displayed by the editor widget
        // it's all distorted like if there was some kind of buffer offset issue (the vertex does not follow the correct bone smh)
        // influence is correct, bind pose data is correct and never updated anyway, pose data for this frame is correct since the widget works
        // maybe gl_VertexID - gl_BaseVertex in the shader does not give the vertex index of the current command?
        // but why only when unknown conditions meet?
        // see DEADLINE_DELIVERY/Content/Blueprints/NPCs/Bus.Bus_C
    }

    protected override void OnComponentUpdate(SkeletalMeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);
        if (!component.IsDirty(DirtyFlags.Animation)) return;

        if (component is { Animation: { } animation, Descriptor.Skeleton: { } skeleton })
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
        }
    }
}
