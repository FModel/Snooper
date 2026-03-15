using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.UObject;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Transforms;

[DefaultActorSystem(typeof(TransformSystem))]
public class SpatialComponent : ActorComponent, IControllable
{
    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Transform;

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
            MarkDirtyUpward(DirtyFlags.Transform);
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

            MarkDirtyUpward(DirtyFlags.Transform);
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
        MarkDirtyUpward(DirtyFlags.Transform); // TODO: fix, for imgui LocalTransform = transform, so we need to force it dirty
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
        MarkDirtyUpward(DirtyFlags.Transform);
    }

    protected virtual bool IsLocalTransformDirty(int index = -1) => _isTransformDirty;

    public virtual Matrix4x4[] GetWorldMatrices(int index = -1) => [WorldMatrix];

    public virtual (Vector3, float) GetTeleportPosition(CameraComponent camera)
    {
        var matrices = GetWorldMatrices();
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

    private void MarkDirtyUpward(DirtyFlags flags)
    {
        MarkDirty(flags);
        Relation?.MarkDirtyUpward(flags);
    }

    internal override string Icon => "\uf601";

    private int _instanceIndex = -1; // -1 = pivot, 0..N-1 = instance index
    public virtual void DrawControls()
    {
        var open = ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap);

        var isPivot = _instanceIndex < 0;
        var isDirty = IsLocalTransformDirty(_instanceIndex);

        // ── Reset button ──────────────────────────────────────────────────────
        var btnSize  = new Vector2(ImGui.GetFrameHeight());
        var resetPos = new Vector2(ImGui.GetItemRectMin().X + ImGui.GetItemRectSize().X - btnSize.X - ImGui.GetStyle().FramePadding.X, ImGui.GetItemRectMin().Y);
        ImGui.SetCursorScreenPos(resetPos);

        if (!isDirty) ImGui.BeginDisabled();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(1f, 1f, 1f, 0.15f));
        if (isDirty) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.55f, 0.20f, 1f));

        if (ImGui.Button("\uf0e2##ResetTransform", btnSize))
        {
            ResetLocalTransform(_instanceIndex);
        }

        if (isDirty) ImGui.PopStyleColor();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
        if (!isDirty) ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(isDirty ? "Reset to original transform" : "No changes to reset");

        if (!open) return;
        ImGui.Indent();

        // ── Transform table ───────────────────────────────────────────────────
        if (!ImGui.BeginTable("TransformControlsTable", 3, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.Unindent();
            return;
        }

        ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Value",    ImGuiTableColumnFlags.WidthStretch, 2.0f);
        ImGui.TableSetupColumn("Flags",    ImGuiTableColumnFlags.WidthFixed,   btnSize.X * 2.2f);

        // ── Target selector (only shown when instanced) ───────────────────────
        if (InstanceCount > 1)
        {
            EditorUI.Property($"Instances ({InstanceCount})");

            var arrowW  = ImGui.GetFrameHeight();
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var inputW  = ImGui.CalcTextSize($"{InstanceCount}").X + ImGui.GetStyle().FramePadding.X * 2f + 20f;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);

            if (ImGui.Button("\uf060##InstPrev", new Vector2(arrowW, arrowW)))
                _instanceIndex = _instanceIndex <= -1 ? InstanceCount - 1 : _instanceIndex - 1;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Previous");

            ImGui.SameLine(0, spacing);

            // InputInt — display: -1 = Pivot, 1..N = Instance 1..N (1-based)
            // internal: _instanceIndex = -1 (Pivot), 0..N-1 (instances)
            var isDirtyInst  = IsLocalTransformDirty(_instanceIndex);
            var displayVal   = _instanceIndex < 0 ? -1 : _instanceIndex + 1; // 0-based → 1-based
            ImGui.SetNextItemWidth(inputW);
            if (isDirtyInst) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0.2f, 1f));
            if (ImGui.InputInt("##InstInput", ref displayVal, 0, 0))
                _instanceIndex = displayVal < 0 ? -1 : Math.Clamp(displayVal - 1, 0, InstanceCount - 1);
            if (isDirtyInst) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_instanceIndex < 0 ? "Pivot" : $"Instance {_instanceIndex + 1} of {InstanceCount}");

            ImGui.SameLine(0, spacing);

            if (ImGui.Button("\uf061##InstNext", new Vector2(arrowW, arrowW)))
                _instanceIndex = _instanceIndex >= InstanceCount - 1 ? -1 : _instanceIndex + 1;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Next");

            ImGui.PopStyleVar();
        }

        // Resolve which transform we're editing
        var t = GetLocalTransform(_instanceIndex);
        var edited  = false;

        // ── Position ──────────────────────────────────────────────────────────
        EditorUI.Property("Position");
        edited |= EditorUI.DragFloat3Axes("Pos", ref t.Position);
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(!isPivot);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f, ImGui.GetStyle().ItemSpacing.Y));
        edited |= EditorUI.ToggleButtonSquare("AbsPos", "\uf05b", ref _absPosition, new Vector4(0.20f, 0.55f, 0.85f, 1f));
        ImGui.PopStyleVar();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(isPivot ? "Absolute Position" + (_absPosition ? " (ON)" : " (OFF)") : "Only available for Pivot");

        // ── Rotation ──────────────────────────────────────────────────────────
        EditorUI.Property("Rotation");
        edited |= EditorUI.DragFloat4Axes("Rot", ref t.Rotation);
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(!isPivot);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f, ImGui.GetStyle().ItemSpacing.Y));
        edited |= EditorUI.ToggleButtonSquare("AbsRot", "\uf2f1", ref _absRotation, new Vector4(0.20f, 0.55f, 0.85f, 1f));
        ImGui.PopStyleVar();
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(isPivot ? "Absolute Rotation" + (_absRotation ? " (ON)" : " (OFF)") : "Only available for Pivot");

        // ── Scale ─────────────────────────────────────────────────────────────
        EditorUI.Property("Scale");
        edited |= EditorUI.DragFloat3AxesLinked("Scale", ref t.Scale, _uniformScale, out _, 0.01f, 0.0001f);
        ImGui.TableSetColumnIndex(2);
        ImGui.BeginDisabled(!isPivot);
        edited |= EditorUI.ToggleButtonSquare("AbsScale", "\uf065", ref _absScale, new Vector4(0.20f, 0.55f, 0.85f, 1f));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(isPivot ? "Absolute Scale" + (_absScale ? " (ON)" : " (OFF)") : "Only available for Pivot");
        ImGui.SameLine(0, 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f, ImGui.GetStyle().ItemSpacing.Y));
        EditorUI.ToggleButtonSquare("LinkScale", _uniformScale ? "\uf0c1" : "\uf127", ref _uniformScale);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(_uniformScale ? "Uniform Scale" : "Non-Uniform Scale");
        ImGui.PopStyleVar();

        // ── Relation info (pivot only) ────────────────────────────────────────
        if (isPivot && Relation is ActorComponent relation)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Attached To");
            ImGui.TableSetColumnIndex(1);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(relation.Name);
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            ImGui.TextUnformatted($"  ({(relation.Actor == Actor ? "Self" : relation.Actor?.Name ?? "Unknown")})");
            ImGui.PopStyleColor();
        }

        ImGui.EndTable();

        if (edited)
        {
            SetLocalTransform(t, _instanceIndex);
        }

        ImGui.Unindent();
    }
}
