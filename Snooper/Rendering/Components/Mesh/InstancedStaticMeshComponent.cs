using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedStaticMeshComponent : StaticMeshComponent
{
    public readonly List<Transform> LocalInstancedTransforms = [];

    protected override int InstanceCount => LocalInstancedTransforms.Count;

    private readonly Transform[] _originalInstancedTransforms;
    private readonly bool[] _instanceDirtyFlags;

    public InstancedStaticMeshComponent(UStaticMesh staticMesh, UInstancedStaticMeshComponent component) : base(staticMesh, component)
    {
        var instances = component.GetInstances();
        foreach (var data in instances)
        {
            LocalInstancedTransforms.Add(data.TransformData);
        }

        IsVisible = IsVisible && LocalInstancedTransforms.Count > 0;

        if (LocalInstancedTransforms.Count == 0)
        {
            // add a dummy instance to avoid issues with empty instance arrays
            // this also makes it possible to toggle visibility on/off without having to add/remove instances
            LocalInstancedTransforms.Add(Transform.Identity);
        }

        _originalInstancedTransforms = new Transform[LocalInstancedTransforms.Count];
        LocalInstancedTransforms.CopyTo(_originalInstancedTransforms);
        _instanceDirtyFlags = new bool[LocalInstancedTransforms.Count];
    }

    public override Transform GetLocalTransform(int index = -1) => index < 0 ? base.GetLocalTransform(index) : LocalInstancedTransforms[index];
    public override void SetLocalTransform(Transform transform, int index = -1)
    {
        if (index < 0) base.SetLocalTransform(transform, index);
        else
        {
            LocalInstancedTransforms[index] = transform;
            _instanceDirtyFlags[index] = true;
            MarkDirty(DirtyFlags.InstanceData);
        }
    }

    protected override void ResetLocalTransform(int index = -1)
    {
        if (index < 0) base.ResetLocalTransform(index);
        else
        {
            var orig = _originalInstancedTransforms[index];
            LocalInstancedTransforms[index].Position = orig.Position;
            LocalInstancedTransforms[index].Rotation = orig.Rotation;
            LocalInstancedTransforms[index].Scale    = orig.Scale;
            _instanceDirtyFlags[index] = false;
            MarkDirty(DirtyFlags.InstanceData);
        }
    }

    protected override bool IsLocalTransformDirty(int index = -1) => index < 0 ? base.IsLocalTransformDirty(index) : _instanceDirtyFlags[index];

    protected override Matrix4x4[] GetWorldMatrices(int index = -1)
    {
        if (index >= 0 && index < LocalInstancedTransforms.Count)
            return [LocalInstancedTransforms[index].ToMatrix() * WorldMatrix];

        var matrices = new Matrix4x4[LocalInstancedTransforms.Count];
        for (var i = 0; i < LocalInstancedTransforms.Count; i++)
        {
            matrices[i] = LocalInstancedTransforms[i].ToMatrix() * WorldMatrix;
        }
        return matrices;
    }
}
