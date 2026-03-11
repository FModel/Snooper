using CUE4Parse.UE4.Assets.Exports.Animation;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Descriptors;

public class SkeletonDescriptor(FReferenceSkeleton reference)
{
    public void Test()
    {
        // BoneTransforms = new Transform[reference.FinalRefBonePose.Length];
        // foreach (var boneIndex in reference.FinalNameToIndexMap.Values)
        // {
        //     BoneTransforms[boneIndex] = new Transform(reference.FinalRefBonePose[boneIndex]);
        // }
    }
}
