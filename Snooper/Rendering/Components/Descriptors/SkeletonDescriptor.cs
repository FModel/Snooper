using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public readonly struct BoneDescriptor(string name, int parentIndex, Matrix4x4 bindPoseLocalMatrix)
{
    public readonly string Name = name;
    public readonly int ParentIndex = parentIndex;
    public readonly Matrix4x4 BindPoseLocalMatrix = bindPoseLocalMatrix;
}

public class SkeletonDescriptor
{
    /// <summary>
    /// local-space transform for each bone for the current frame. This is the single source of truth for bone transforms.
    /// </summary>
    public Matrix4x4[] BoneLocalMatrices { get; }

    /// <summary>
    /// this is never modified after construction.
    /// </summary>
    public BoneDescriptor[] BoneDescriptors { get; }

    /// <summary>
    /// model-space transform for each bone for the current frame. This is always recalculated from BoneLocalMatrices.
    /// Never set this array directly.
    /// </summary>
    public Matrix4x4[] BoneMatrices { get; }

    public IReadOnlyDictionary<string, uint> BoneNameToIndex => _boneNameToIndex;
    private readonly Dictionary<string, uint> _boneNameToIndex;

    public int BoneCount => BoneLocalMatrices.Length;

    public SkeletonDescriptor(FReferenceSkeleton reference)
    {
        BoneLocalMatrices = new Matrix4x4[reference.FinalRefBonePose.Length];
        BoneDescriptors = new BoneDescriptor[BoneCount];
        BoneMatrices = new Matrix4x4[BoneCount];
        _boneNameToIndex = new Dictionary<string, uint>(BoneCount, StringComparer.OrdinalIgnoreCase);

        for (var boneIndex = 0u; boneIndex < BoneCount; boneIndex++)
        {
            var info = reference.FinalRefBoneInfo[boneIndex];
            var matrix = new Transform(reference.FinalRefBonePose[boneIndex]).ToMatrix();
            var descriptor = new BoneDescriptor(info.Name.Text, info.ParentIndex, matrix);

            BoneLocalMatrices[boneIndex] = descriptor.BindPoseLocalMatrix;
            BoneDescriptors[boneIndex] = descriptor;
            _boneNameToIndex.Add(descriptor.Name, boneIndex);
        }

        RecalculateBoneMatrices();
    }

    public event Action? OnBoneMatricesChanged;

    public string GetBoneName(int index) => BoneDescriptors[index].Name;
    public int GetBoneParentIndex(int index) => BoneDescriptors[index].ParentIndex;

    public void MoveBone(int boneIndex, Matrix4x4 matrix)
    {
        var pi = BoneDescriptors[boneIndex].ParentIndex;
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

    public void ResetBone(int boneIndex)
    {
        BoneLocalMatrices[boneIndex] = BoneDescriptors[boneIndex].BindPoseLocalMatrix;
        RecalculateBoneMatrices(boneIndex);
    }

    public void ResetAllBones()
    {
        for (var i = 0; i < BoneCount; i++)
        {
            BoneLocalMatrices[i] = BoneDescriptors[i].BindPoseLocalMatrix;
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
            BoneMatrices[i] = pi < 0 ? BoneLocalMatrices[i] : BoneLocalMatrices[i] * BoneMatrices[pi];
        }

        OnBoneMatricesChanged?.Invoke();
    }
}
