using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent : ActorComponent, IControllable
{
    public SpatialComponent(Transform? transform = null, string? name = null) : base(name)
    {
        LocalTransform = transform ?? Transform.Identity;
    }

    public SpatialComponent(UActorComponent component) : base(component)
    {
        LocalTransform = Transform.Identity;
    }

    public SpatialComponent(USceneComponent component) : base(component)
    {
        LocalTransform = component.GetRelativeTransform();

        _absPosition = component.GetOrDefault<bool>("bAbsoluteLocation");
        _absRotation = component.GetOrDefault<bool>("bAbsoluteRotation");
        _absScale = component.GetOrDefault<bool>("bAbsoluteScale");
    }

    private readonly bool _absPosition;
    private readonly bool _absRotation;
    private readonly bool _absScale;

    private Transform _localTransform = Transform.Identity;
    public Transform LocalTransform
    {
        get => _localTransform;
        set
        {
            if (_localTransform == value)
                return;

            _localTransform = value;
            MarkDirtyUpward(DirtyFlags.Transform);
        }
    }

    private SpatialComponent? _relation;
    public SpatialComponent? Relation
    {
        get => _relation;
        set
        {
            if (this == value || _relation == value) return;

            _relation?.Children.Remove(this);
            _relation = value;

            if (_relation != null && !_relation.Children.Contains(this))
                _relation.Children.Add(this);

            MarkDirtyUpward(DirtyFlags.Transform);
        }
    }

    public readonly List<SpatialComponent> Children = [];

    public Matrix4x4 WorldMatrix { get; private set; } = Matrix4x4.Identity;

    public virtual Matrix4x4[] GetInstanceMatrices() => [WorldMatrix];

    public virtual (Vector3, float) GetTeleportPosition(CameraComponent camera)
    {
        var matrices = GetInstanceMatrices();
        if (matrices.Length == 0) return (Vector3.Zero, 1.0f);

        var center = Vector3.Zero;
        foreach (var matrix in matrices)
        {
            center += matrix.Translation;
        }
        return (center / matrices.Length, 2.50f);
    }

    public void UpdateWorldMatrix(bool recursive = true)
    {
        if (Relation is null)
        {
            WorldMatrix = LocalTransform.ToMatrix();
        }
        else
        {
            if (recursive) Relation.UpdateWorldMatrix();
            if (!_absPosition && !_absRotation && !_absScale)
            {
                WorldMatrix = LocalTransform.ToMatrix() * Relation.WorldMatrix;
            }
            else
            {
                Matrix4x4.Decompose(Relation.WorldMatrix, out var scale, out var rotation, out _);

                WorldMatrix = new Transform
                {
                    Position = _absPosition ? LocalTransform.Position : Vector3.Transform(LocalTransform.Position, Relation.WorldMatrix),
                    Rotation = _absRotation ? LocalTransform.Rotation : rotation * LocalTransform.Rotation,
                    Scale = _absScale ? LocalTransform.Scale : LocalTransform.Scale * scale
                }.ToMatrix();
            }
        }

        // this component's WorldMatrix is now clean and needs to be updated on GPU
        MarkClean(DirtyFlags.Transform);
        MarkDirty(DirtyFlags.InstanceData);

        // since this component's WorldMatrix changed, all children need to update theirs too
        foreach (var child in Children)
        {
            child.MarkDirty(DirtyFlags.Transform);
        }
    }

    private void MarkDirtyUpward(DirtyFlags flags)
    {
        MarkDirty(flags);
        Relation?.MarkDirtyUpward(flags);
    }

    internal override string Icon => "perspective";

    public virtual void DrawControls()
    {
        EditorUI.CollapsingTable("Transform", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            var edited = EditorUI.DragFloat3("Position", ref LocalTransform.Position);
            edited |= EditorUI.DragFloat4("Rotation", ref LocalTransform.Rotation);
            edited |= EditorUI.DragFloat3("Scale", ref LocalTransform.Scale, 0.1f, 0.01f);
            if (edited)
            {
                MarkDirtyUpward(DirtyFlags.Transform);
            }

            if (Relation is ActorComponent relation)
            {
                EditorUI.Text("Attached To", $"{relation.Name} in {(relation.Actor == Actor ? "Self" : relation.Actor?.Name ?? "Unknown")}");
            }
        });
    }
}
