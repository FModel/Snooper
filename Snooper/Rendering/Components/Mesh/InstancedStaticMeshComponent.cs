using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Components.Mesh;

public class InstancedStaticMeshComponent : StaticMeshComponent
{
    public InstancedTransforms Instances { get; }

    public override int InstanceCount => Instances.Count;

    public InstancedStaticMeshComponent(UStaticMesh staticMesh, UInstancedStaticMeshComponent component) : this(staticMesh, component, component.GetInstances())
    {

    }

    protected InstancedStaticMeshComponent(UStaticMesh staticMesh, UInstancedStaticMeshComponent component, FInstancedStaticMeshInstanceData[] instances) : base(staticMesh, component)
    {
        var transforms = new Transform[instances.Length];
        for (var i = 0; i < transforms.Length; i++)
        {
            transforms[i] = instances[i].TransformData;
        }

        var cullDistance = component.InstanceEndCullDistance * Settings.GlobalScale;
        if (cullDistance > 0.0f && (DrawDistance.Y == 0.0f || cullDistance < DrawDistance.Y))
        {
            DrawDistance = DrawDistance with { Y = cullDistance };
        }

        Instances = new InstancedTransforms(transforms);
        IsVisible = IsVisible && instances.Length > 0;
    }

    public InstancedStaticMeshComponent(UStaticMesh staticMesh, List<Transform> transforms) : base(staticMesh)
    {
        Instances = new InstancedTransforms([.. transforms]);
        IsVisible = IsVisible && transforms.Count > 0;
    }

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
