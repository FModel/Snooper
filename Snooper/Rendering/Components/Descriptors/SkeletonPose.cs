using System.Numerics;
using Snooper.Core.Containers.Buffers;

namespace Snooper.Rendering.Components.Descriptors;

public class SkeletonPose
{
    public readonly SkeletonDescriptor Skeleton;

    internal BufferAllocation? _poseAllocation;

    /// <summary>
    /// local-space transform for each bone for the current frame. This is the single source of truth for bone transforms.
    /// </summary>
    public Matrix4x4[] BoneLocalMatrices { get; }

    /// <summary>
    /// model-space transform for each bone for the current frame. This is always recalculated from BoneLocalMatrices.
    /// Never set this array directly.
    /// </summary>
    public Matrix4x4[] BoneMatrices { get; }

    public int BoneCount => BoneLocalMatrices.Length;

    public SkeletonPose(SkeletonDescriptor skeleton)
    {
        Skeleton = skeleton;
        BoneLocalMatrices = new Matrix4x4[skeleton.BoneCount];
        BoneMatrices = (Matrix4x4[]) skeleton.BindPoseMatrices.Clone();
        for (var i = 0; i < BoneCount; i++)
        {
            BoneLocalMatrices[i] = skeleton.BoneDescriptors[i].BindPoseLocalMatrix;
        }
    }

    public void MoveBone(int boneIndex, Matrix4x4 matrix)
    {
        var pi = Skeleton.BoneDescriptors[boneIndex].ParentIndex;
        if (pi >= 0 && Matrix4x4.Invert(BoneMatrices[pi], out var parentMatrix))
        {
            BoneLocalMatrices[boneIndex] = matrix * parentMatrix;
        }
        else
        {
            BoneLocalMatrices[boneIndex] = matrix;
        }

        RecalculateBoneMatrices(boneIndex);
    }

    public bool IsBoneEdited(int boneIndex) => BoneLocalMatrices[boneIndex] != Skeleton.BoneDescriptors[boneIndex].BindPoseLocalMatrix;

    public void ResetBone(int boneIndex)
    {
        BoneLocalMatrices[boneIndex] = Skeleton.BoneDescriptors[boneIndex].BindPoseLocalMatrix;
        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetAllBones()
    {
        for (var i = 0; i < BoneCount; i++)
        {
            BoneLocalMatrices[i] = Skeleton.BoneDescriptors[i].BindPoseLocalMatrix;
        }
        RecalculateBoneMatrices();
    }

    public void Follow(SkeletonPose leader, int[] boneMap)
    {
        for (var i = 0; i < BoneCount; i++)
        {
            var bone = Skeleton.BoneDescriptors[i];
            BoneMatrices[i] = boneMap[i] >= 0 ? leader.BoneMatrices[boneMap[i]]
                : bone.IsRoot ? bone.BindPoseLocalMatrix : bone.BindPoseLocalMatrix * BoneMatrices[bone.ParentIndex];
        }
    }

    public void RecalculateBoneMatrices(int start = -1, int end = -1)
    {
        var from = start >= 0 ? start : 0;
        var to = end >= 0 && end < BoneCount ? end : BoneCount - 1;
        for (var i = from; i <= to; i++)
        {
            var pi = Skeleton.BoneDescriptors[i].ParentIndex;
            BoneMatrices[i] = pi < 0 ? BoneLocalMatrices[i] : BoneLocalMatrices[i] * BoneMatrices[pi];
        }
    }
}
