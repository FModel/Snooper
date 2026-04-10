using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.UObject;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent : ActorComponent
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Transform;

    protected SpatialComponent(SpatialComponent other) : base(other)
    {
        LocalTransform = (Transform) other.LocalTransform.Clone();
        AttachSocketName = other.AttachSocketName;

        _absPosition = other._absPosition;
        _absRotation = other._absRotation;
        _absScale = other._absScale;

        _originalAbsPosition = other._originalAbsPosition;
        _originalAbsRotation = other._originalAbsRotation;
        _originalAbsScale = other._originalAbsScale;
        _originalTransform = (Transform) other._originalTransform.Clone();
    }

    public SpatialComponent(Transform? transform = null, string? name = null) : base(name)
    {
        LocalTransform = transform ?? Transform.Identity;
        _originalTransform = Snapshot();
    }

    public SpatialComponent(UActorComponent component) : base(component)
    {
        LocalTransform = Transform.Identity;
        _originalTransform = Snapshot();
    }

    public SpatialComponent(USceneComponent component) : base(component)
    {
        LocalTransform = component.GetRelativeTransform();
        AttachSocketName = component.GetOrDefault<FName?>("AttachSocketName")?.Text;

        _absPosition = component.GetOrDefault<bool>("bAbsoluteLocation");
        _absRotation = component.GetOrDefault<bool>("bAbsoluteRotation");
        _absScale = component.GetOrDefault<bool>("bAbsoluteScale");

        _originalAbsPosition = _absPosition;
        _originalAbsRotation = _absRotation;
        _originalAbsScale = _absScale;
        _originalTransform = Snapshot();
    }

    private Transform Snapshot() => new() { Position = LocalTransform.Position, Rotation = LocalTransform.Rotation, Scale = LocalTransform.Scale };

    private bool _absPosition;
    private bool _absRotation;
    private bool _absScale;
    private bool _uniformScale = true;

    private readonly Transform _originalTransform;
    private readonly bool _originalAbsPosition;
    private readonly bool _originalAbsRotation;
    private readonly bool _originalAbsScale;
    private bool _isTransformDirty;

    protected virtual int InstanceCount => 1;

    public string? AttachSocketName;

    public Transform LocalTransform
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            MarkDirty(DirtyFlags.Transform);
        }
    }

    public SpatialComponent? Relation
    {
        get;
        set
        {
            if (this == value || field == value) return;

            field?.Children.Remove(this);
            field = value;

            if (field != null && !field.Children.Contains(this))
                field.Children.Add(this);

            MarkDirty(DirtyFlags.Transform);
        }
    }

    public readonly List<SpatialComponent> Children = [];

    public Matrix4x4 WorldMatrix { get; private set; } = Matrix4x4.Identity;

    public Matrix4x4 GizmoMatrix
    {
        get
        {
            if (InstanceCount > 1 && _instanceIndex >= 0 && _instanceIndex < InstanceCount)
            {
                return GetWorldMatrices(_instanceIndex)[0]; // Specific instance selected — gizmo sits at that instance's origin.
            }
            return WorldMatrix; // No instance selected — gizmo sits at the origin (or for instances, the pivot's origin).

            // TODO: if offsetting gizmo is implemented, avoid calling GetWorldMatrices without an index
            // because for large instances, it's gonna send you a list of 50k matrices and you don't want that every frame trust me

            // var matrices = GetWorldMatrices();
            // if (matrices.Length < 2)
            //     return matrices[0]; // No instances — gizmo sits at the origin.
            //
            // if (_instanceIndex >= 0 && _instanceIndex < matrices.Length)
            //     return matrices[_instanceIndex]; // Specific instance selected — gizmo sits at that instance's origin.
            //
            // // No instance selected — gizmo sits at the centroid.
            // var center = Vector3.Zero;
            // foreach (var m in matrices) center += m.Translation;
            // return Matrix4x4.CreateTranslation(center / matrices.Length);
        }
    }

    public void ApplyGizmoMatrix(Matrix4x4 manipulated)
    {
        // TODO: support gizmo offsets, but any manipulation to GizmoMatrix must be undone here to get the new LocalTransform (see centroid shit)

        if (_instanceIndex >= 0)
        {
            Matrix4x4.Invert(WorldMatrix, out var invPivot);
            Matrix4x4.Decompose(manipulated * invPivot, out var iScale, out var iRotation, out var iPosition);
            SetLocalTransform(new Transform { Scale = iScale, Rotation = iRotation, Position = iPosition }, _instanceIndex);
        }
        else
        {
            var invRelation = Matrix4x4.Identity;
            if (Relation is { } relation)
                Matrix4x4.Invert(relation.WorldMatrix, out invRelation);

            Matrix4x4.Decompose(manipulated * invRelation, out var pScale, out var pRotation, out var pPosition);
            SetLocalTransform(new Transform { Scale = pScale, Rotation = pRotation, Position = pPosition });
        }
    }

    public virtual Transform GetLocalTransform(int index = -1) => LocalTransform;
    public virtual void SetLocalTransform(Transform transform, int index = -1)
    {
        LocalTransform = transform;
        _isTransformDirty = true;
        MarkDirty(DirtyFlags.Transform); // TODO: fix, for imgui LocalTransform = transform, so we need to force it dirty
    }

    protected virtual void ResetLocalTransform(int index = -1)
    {
        LocalTransform.Position = _originalTransform.Position;
        LocalTransform.Rotation = _originalTransform.Rotation;
        LocalTransform.Scale    = _originalTransform.Scale;
        _absPosition = _originalAbsPosition;
        _absRotation = _originalAbsRotation;
        _absScale    = _originalAbsScale;
        _isTransformDirty = false;
        MarkDirty(DirtyFlags.Transform);
    }

    protected virtual bool IsLocalTransformDirty(int index = -1) => _isTransformDirty;

    public virtual Matrix4x4[] GetWorldMatrices(int index = -1) => [WorldMatrix];

    protected virtual (Vector3, float) GetTeleportPosition(CameraComponent camera) => (GizmoMatrix.Translation, 2.50f);

    public void TeleportTo()
    {
        if (Actor?.ActorManager is not SceneManager { MainViewport.Camera: { } camera })
            return;

        var (center, distance) = GetTeleportPosition(camera);
        camera.TeleportTo(center, distance);
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

            var relationMatrix = Relation.WorldMatrix;
            if (!string.IsNullOrEmpty(AttachSocketName) && Relation is MeshComponent mesh)
            {
                relationMatrix = mesh.Descriptor.GetSocketModelMatrix(AttachSocketName) * relationMatrix;
            }

            if (!_absPosition && !_absRotation && !_absScale)
            {
                WorldMatrix = LocalTransform.ToMatrix() * relationMatrix;
            }
            else
            {
                Matrix4x4.Decompose(relationMatrix, out var scale, out var rotation, out _);

                WorldMatrix = new Transform
                {
                    Position = _absPosition ? LocalTransform.Position : Vector3.Transform(LocalTransform.Position, relationMatrix),
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

    #region UI
    public override string Icon => "\uf601";
    public override bool ShouldScrollHere
    {
        get;
        set
        {
            field = value;
            Relation?.ShouldScrollHere = field;

            if (field) IsNodeOpen = true;
        }
    }

    private const string HeaderLabel = "Transform";
    private HeaderButtons HeaderButtons => field ??= new HeaderButtons(HeaderLabel)
        .Add(
            () => "\uf0e2",
            () => IsLocalTransformDirty(_instanceIndex) ? "Reset to original transform" : "No changes to reset",
            () => ResetLocalTransform(_instanceIndex),
            () => IsLocalTransformDirty(_instanceIndex),
            () => IsLocalTransformDirty(_instanceIndex) ? Settings.OrangeColor : null
        );

    private PropertyToggleButton[] InstanceNavButtons => field ??= [
        new PropertyToggleButton(
            () => "\uf104",
            () => { _instanceIndex = _instanceIndex < 0 ? InstanceCount - 1 : _instanceIndex - 1; TeleportTo(); },
            () => "Previous"
        ),
        new PropertyToggleButton(
            () => "\uf105",
            () => { _instanceIndex = _instanceIndex >= InstanceCount - 1 ? -1 : _instanceIndex + 1; TeleportTo(); },
            () => "Next"
        )
    ];
    private PropertyToggleButton[] PositionButtons => field ??= [
        new PropertyToggleButton(
            () => _absPosition ? "\uf023" : "\uf3c1",
            () => { _absPosition = !_absPosition; _isTransformDirty = true; MarkDirty(DirtyFlags.Transform); },
            () => _absPosition ? "Absolute Position\nClick to make relative" : "Relative Position\nClick to make absolute"
        )
    ];
    private PropertyToggleButton[] RotationButtons => field ??= [
        new PropertyToggleButton(
            () => _absRotation ? "\uf023" : "\uf3c1",
            () => { _absRotation = !_absRotation; _isTransformDirty = true; MarkDirty(DirtyFlags.Transform); },
            () => _absRotation ? "Absolute Rotation\nClick to make relative" : "Relative Rotation\nClick to make absolute"
        )
    ];
    private PropertyToggleButton[] ScaleButtons => field ??= [
        new PropertyToggleButton(
            () => _uniformScale ? "\uf0c1" : "\uf127",
            () => _uniformScale = !_uniformScale,
            () => _uniformScale ? "Uniform Scale\nClick to allow non-uniform" : "Non-Uniform Scale\nClick to link axes"
        ),
        new PropertyToggleButton(
            () => _absScale ? "\uf023" : "\uf3c1",
            () => { _absScale = !_absScale; _isTransformDirty = true; MarkDirty(DirtyFlags.Transform); },
            () => _absScale ? "Absolute Scale\nClick to make relative" : "Relative Scale\nClick to make absolute"
        )
    ];

    private int _instanceIndex = -1; // -1 = pivot, 0..N-1 = instance index
    public override void DrawControls()
    {
        base.DrawControls();

        var open = ImGui.CollapsingHeader(HeaderLabel, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);
        HeaderButtons.Draw(ImGui.GetItemRectMin(), ImGui.GetItemRectSize());

        if (!open) return;

        EditorUI.PropertyValueTable(HeaderLabel, () =>
        {
            var isPivot = _instanceIndex < 0;

            if (InstanceCount > 1)
            {
                EditorUI.PropertyWithToggle($"Instance ({InstanceCount})", InstanceNavButtons);

                var displayVal = _instanceIndex < 0 ? 0 : _instanceIndex + 1;
                if (ImGui.InputInt("##InstInput", ref displayVal, 0, 0))
                {
                    displayVal = Math.Clamp(displayVal, 0, InstanceCount);
                    _instanceIndex = displayVal == 0 ? -1 : displayVal - 1;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted($"0 = Pivot\n1..{InstanceCount} = Instance Index");
                    ImGui.SetWindowFontScale(0.85f);
                    ImGui.Spacing();
                    ImGui.TextDisabled("Changes to pivot transform will affect all instances");
                    ImGui.SetWindowFontScale(1.0f);
                    ImGui.EndTooltip();
                }
            }

            var edited  = false;
            var t = GetLocalTransform(_instanceIndex);

            EditorUI.PropertyWithToggle("Position", PositionButtons);
            edited |= EditorUI.DragAxes("Position", ref t.Position);

            EditorUI.PropertyWithToggle("Rotation", RotationButtons);
            edited |= EditorUI.DragAxes("Rotation", ref t.Rotation);

            EditorUI.PropertyWithToggle("Scale", ScaleButtons);
            edited |= EditorUI.DragAxes("Scale", ref t.Scale, _uniformScale, out _, 0.01f, 0.0001f);

            if (isPivot && !string.IsNullOrEmpty(AttachSocketName))
            {
                EditorUI.Property("Attached To");
                ImGui.TextUnformatted(AttachSocketName);
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                ImGui.TextUnformatted($"(in {Relation?.Name ?? "Unknown"})");
                ImGui.PopStyleColor();
            }

            if (edited) SetLocalTransform(t, _instanceIndex);
        });
    }
    #endregion

    public override object Clone() => new SpatialComponent(this);
}
