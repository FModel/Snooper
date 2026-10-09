using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedSkinnedMeshComponent : SkinnedMeshComponent
{
    public InstancedTransforms Instances { get; }

    public override int InstanceCount => Instances.Count;

    public InstancedSkinnedMeshComponent(USkeletalMesh skeletalMesh, UInstancedSkinnedMeshComponent component) : base(skeletalMesh, component)
    {
        var instances = component.InstanceData;
        var transforms = new Transform[instances.Length];
        for (var i = 0; i < transforms.Length; i++)
        {
            transforms[i] = instances[i].Transform;
        }

        Instances = new InstancedTransforms(transforms);
        IsVisible = IsVisible && instances.Length > 0;
    }

    protected override DebugComponent CreateDebugVisualization() => new InstancedMeshBoundsVisualization(this, Instances);

    public override Transform GetLocalTransform(int index = -1) => index < 0 ? base.GetLocalTransform(index) : Instances[index];
    public override void SetLocalTransform(Transform transform, int index = -1)
    {
        if (index < 0) base.SetLocalTransform(transform, index);
        else
        {
            Instances.Set(index, transform);
            MarkDirty(DirtyFlags.InstanceData);
        }
    }

    protected override void ResetLocalTransform(int index = -1)
    {
        if (index < 0) base.ResetLocalTransform(index);
        else
        {
            Instances.Reset(index);
            MarkDirty(DirtyFlags.InstanceData);
        }
    }

    protected override bool IsLocalTransformDirty(int index = -1) => index < 0 ? base.IsLocalTransformDirty(index) : Instances.IsDirty(index);

    public override Matrix4x4[] GetWorldMatrices(int index = -1) => Instances.GetWorldMatrices(WorldMatrix, index);
}
