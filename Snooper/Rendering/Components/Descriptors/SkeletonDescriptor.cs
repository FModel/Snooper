using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public record BoneDescriptor(string Name, int ParentIndex, Matrix4x4 BindPoseLocalMatrix);

public class SkeletonDescriptor
{
    public Matrix4x4[] BoneMatrices { get; } // model space relative to the parent bone
    public BoneDescriptor[] BoneDescriptors { get; }
    public Dictionary<string, uint> BoneNameToIndex { get; }

    public int BoneCount => BoneMatrices.Length;

    private readonly Matrix4x4[] _boneLocalMatrices;

    public SkeletonDescriptor(FReferenceSkeleton reference)
    {
        BoneMatrices = new Matrix4x4[reference.FinalRefBonePose.Length];
        BoneNameToIndex = new Dictionary<string, uint>(BoneCount, StringComparer.OrdinalIgnoreCase);
        BoneDescriptors = new BoneDescriptor[BoneCount];

        _boneLocalMatrices = new Matrix4x4[BoneCount];

        for (var boneIndex = 0u; boneIndex < BoneCount; boneIndex++)
        {
            var info = reference.FinalRefBoneInfo[boneIndex];
            var matrix = new Transform(reference.FinalRefBonePose[boneIndex]).ToMatrix();

            BoneNameToIndex.Add(info.Name.Text, boneIndex);
            BoneDescriptors[boneIndex] = new BoneDescriptor(info.Name.Text, info.ParentIndex, matrix);

            _boneLocalMatrices[boneIndex] = matrix;
        }

        RecalculateBoneMatrices();
    }

    public event Action? OnBoneMatricesChanged;

    public Matrix4x4 GetBoneModelMatrix(string boneName) => BoneMatrices[BoneNameToIndex[boneName]];
    public string GetBoneName(int index) => BoneDescriptors[index].Name;
    public int GetBoneParentIndex(int index) => BoneDescriptors[index].ParentIndex;

    public void MoveBone(int boneIndex, Matrix4x4 matrix)
    {
        var pi = BoneDescriptors[boneIndex].ParentIndex;
        if (pi >= 0 && Matrix4x4.Invert(BoneMatrices[pi], out var parentMatrix))
        {
            _boneLocalMatrices[boneIndex] = matrix * parentMatrix;
        }
        else
        {
            _boneLocalMatrices[boneIndex] = matrix;
        }

        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetBone(int boneIndex)
    {
        _boneLocalMatrices[boneIndex] = BoneDescriptors[boneIndex].BindPoseLocalMatrix;
        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetAllBones()
    {
        for (var i = 0; i < BoneCount; i++)
        {
            _boneLocalMatrices[i] = BoneDescriptors[i].BindPoseLocalMatrix;
        }

        RecalculateBoneMatrices();
    }

    private void RecalculateBoneMatrices(int start = -1, int end = -1)
    {
        var from = start >= 0 ? start : 0;
        var to = end >= 0 && end < BoneCount ? end : BoneCount - 1;
        for (var i = from; i <= to; i++)
        {
            var pi = BoneDescriptors[i].ParentIndex;
            BoneMatrices[i] = pi < 0 ? _boneLocalMatrices[i] : _boneLocalMatrices[i] * BoneMatrices[pi];
        }

        OnBoneMatricesChanged?.Invoke();
    }
}
