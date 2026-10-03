using System.Numerics;
using ImGuiNET;
using ImGuizmoNET;
using Snooper;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

public class SkeletonEditorWidget : IViewportTool<MeshComponent>
{
    private const uint ColDim = 0xFF_D0_D0_D0;
    private const uint ColSocket = 0xFF_80_FF_80;
    private const uint ColSelected = 0xFF_FF_A0_30; // blue, the orange of the selection outline is already on the mesh
    private const uint ColParent = 0xFF_40_E0_FF; // yellow
    private const uint ColHovered = 0xFF_FF_FF_FF;
    private const uint ColShadow = 0xFF_00_00_00;

    private const int None = int.MinValue;

    private object? _owner; // the descriptor everything below was sized and resolved for

    private Vector2[] _screens = [];
    private float[] _depths = []; // view depth, NaN for a bone behind the camera

    private Vector2[] _socketScreens = [];
    private float[] _socketDepths = [];
    private int[] _socketBones = []; // the bone a socket hangs on, -1 for none

    private readonly List<int> _stack = []; // what sits under the cursor, a bone as its index, a socket as ~index

    public bool Draw(in ViewportContext viewport, MeshComponent mesh)
    {
        var skinned = mesh as SkinnedMeshComponent;
        var skeleton = mesh.Descriptor.Skeleton;
        var sockets = mesh.Descriptor.Sockets;
        var bones = skeleton?.BoneCount ?? 0;
        var shown = SkeletonTreeWidget.ShowSockets ? sockets.Length : 0; // sockets follow the toggle of the skeleton tree
        if (bones == 0 && shown == 0) return false;

        var selectedBone = skinned?.SelectedBone ?? -1;
        var selectedSocket = shown > 0 ? skinned?.SelectedSocket ?? -1 : -1;
        var selected = selectedSocket >= 0 ? ~selectedSocket : selectedBone >= 0 ? selectedBone : None;

        var drawList = viewport.DrawList;
        var min = viewport.Position;
        var size = viewport.Size;
        var unit = ImGui.GetFrameHeight();
        var view = mesh.WorldMatrix * viewport.Camera.ViewMatrix;
        var projection = viewport.Camera.ProjectionMatrix;

        if (!ReferenceEquals(_owner, mesh.Descriptor)) Resolve();

        for (var i = 0; i < bones; i++)
        {
            Project(skeleton!.BoneMatrices[i].Translation, out _screens[i], out _depths[i]);
        }

        for (var i = 0; i < shown; i++)
        {
            _socketDepths[i] = float.NaN;
            if (sockets[i] is not { } socket) continue;

            var matrix = socket.LocalMatrix;
            if (_socketBones[i] >= 0) matrix *= skeleton!.BoneMatrices[_socketBones[i]];
            Project(matrix.Translation, out _socketScreens[i], out _socketDepths[i]);
        }

        // the gizmo of the selected bone comes first, then whatever is under the cursor, the nearest first
        _stack.Clear();
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(min, min + size, false) && !ImGuizmo.IsUsing() && !(selectedBone >= 0 && ImGuizmo.IsOver()))
        {
            var mouse = ImGui.GetMousePos();
            var reach = unit * 0.4f * (unit * 0.4f);

            for (var i = 0; i < bones; i++)
            {
                if (!float.IsNaN(_depths[i]) && Vector2.DistanceSquared(mouse, _screens[i]) < reach) Stack(i);
            }

            for (var i = 0; i < shown; i++)
            {
                if (!float.IsNaN(_socketDepths[i]) && Vector2.DistanceSquared(mouse, _socketScreens[i]) < reach) Stack(~i);
            }
        }

        // a click takes what follows the selection in the stack, so bones sitting on each other can all be reached
        var target = _stack.Count > 0 ? _stack[(_stack.IndexOf(selected) + 1) % _stack.Count] : None;

        drawList.PushClipRect(min, min + size, true);

        for (var i = 0; i < bones; i++)
        {
            var parent = skeleton!.GetBoneParentIndex(i);
            if (parent < 0 || float.IsNaN(_depths[i]) || float.IsNaN(_depths[parent])) continue;

            // everything is thin, dim and hollow so it blends with the mesh, only the selection and what is under the cursor
            // stand out. A dark line under the light one keeps it readable whatever the mesh looks like: under everything
            // while nothing is selected, then only under the selection so it is the one thing that reads
            var marked = parent == selectedBone || i == selectedBone;
            if (marked || selected == None) drawList.AddLine(_screens[parent], _screens[i], Fade(ColShadow, 0.35f), unit * (marked ? 0.13f : 0.09f));
            if (parent == selectedBone) drawList.AddLine(_screens[parent], _screens[i], ColSelected, unit * 0.07f);
            else if (i == selectedBone) drawList.AddLine(_screens[parent], _screens[i], ColParent, unit * 0.07f);
            else drawList.AddLine(_screens[parent], _screens[i], Fade(ColDim, 0.45f), unit * 0.03f);
        }

        for (var i = 0; i < bones; i++)
        {
            if (float.IsNaN(_depths[i]) || i == selected || i == target) continue;

            if (selected == None) drawList.AddCircle(_screens[i], unit * 0.11f, Fade(ColShadow, 0.5f), 12, unit * 0.11f);
            drawList.AddCircle(_screens[i], unit * 0.11f, Fade(ColDim, 0.7f), 12, unit * 0.05f);
        }

        for (var i = 0; i < shown; i++)
        {
            if (float.IsNaN(_socketDepths[i]) || ~i == selected || ~i == target) continue;

            if (selected == None) Diamond(_socketScreens[i], unit * 0.13f, Fade(ColShadow, 0.5f), unit * 0.11f);
            Diamond(_socketScreens[i], unit * 0.13f, Fade(ColSocket, 0.7f), unit * 0.05f);
        }

        if (target != selected) Mark(target, ColHovered);
        Mark(selected, ColSelected);

        drawList.PopClipRect();

        if (target != None)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(Name(target));
            if (_stack.Count > 1) ImGui.TextDisabled($"{_stack.Count} here, click again for the next one");
            ImGui.EndTooltip();

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (target >= 0) skinned?.SelectedBone = target;
                else skinned?.SelectedSocket = ~target;
            }
        }

        if (skeleton != null && selectedBone >= 0)
        {
            var matrix = skeleton.BoneMatrices[selectedBone] * mesh.GizmoMatrix;
            if (viewport.Manipulate(ref matrix, viewport.Operation, MODE.LOCAL))
            {
                Matrix4x4.Invert(mesh.GizmoMatrix, out var inverse);
                skinned?.MoveBone(selectedBone, matrix * inverse);
            }
        }

        return target != None;

        void Resolve()
        {
            _owner = mesh.Descriptor;

            if (_screens.Length < bones)
            {
                _screens = new Vector2[bones];
                _depths = new float[bones];
            }

            if (_socketBones.Length < sockets.Length)
            {
                _socketScreens = new Vector2[sockets.Length];
                _socketDepths = new float[sockets.Length];
                _socketBones = new int[sockets.Length];
            }

            for (var i = 0; i < sockets.Length; i++)
            {
                var bone = sockets[i] switch
                {
                    SkeletalMeshSocketDescriptor attached => attached.BoneName,
                    { } socket => socket.Name,
                    _ => null
                };

                _socketBones[i] = bone != null && skeleton?.BoneNameToIndex.TryGetValue(bone, out var index) == true ? (int) index : -1;
            }
        }

        bool Project(Vector3 model, out Vector2 screen, out float depth)
        {
            var position = Vector3.Transform(model, view);
            var clip = Vector4.Transform(new Vector4(position, 1f), projection);
            if (clip.W <= 0f)
            {
                screen = default;
                depth = float.NaN;
                return false;
            }

            screen = new Vector2(min.X + (clip.X / clip.W * 0.5f + 0.5f) * size.X, min.Y + (0.5f - clip.Y / clip.W * 0.5f) * size.Y);
            depth = -position.Z;
            return true;
        }

        float DepthOf(int id) => id >= 0 ? _depths[id] : _socketDepths[~id];

        // keeps what is under the cursor ordered from the nearest to the farthest
        void Stack(int id)
        {
            var at = 0;
            while (at < _stack.Count && DepthOf(_stack[at]) <= DepthOf(id)) at++;
            _stack.Insert(at, id);
        }

        void Mark(int id, uint color)
        {
            if (id == None || float.IsNaN(DepthOf(id))) return;

            if (id >= 0)
            {
                drawList.AddCircleFilled(_screens[id], unit * 0.28f, Fade(ColShadow, 0.6f), 12);
                drawList.AddCircleFilled(_screens[id], unit * 0.22f, color, 12);
            }
            else
            {
                Diamond(_socketScreens[~id], unit * 0.33f, Fade(ColShadow, 0.6f));
                Diamond(_socketScreens[~id], unit * 0.26f, color);
            }
        }

        string Name(int id) => id >= 0 ? $"[{id}] {skeleton?.GetBoneName(id)}" : $"{Settings.PlugIcon} {sockets[~id]?.Name}";

        // filled unless a thickness makes it an outline
        void Diamond(Vector2 center, float half, uint color, float thickness = 0f)
        {
            var top = center with { Y = center.Y - half };
            var right = center with { X = center.X + half };
            var bottom = center with { Y = center.Y + half };
            var left = center with { X = center.X - half };

            if (thickness > 0f) drawList.AddQuad(top, right, bottom, left, color, thickness);
            else drawList.AddQuadFilled(top, right, bottom, left, color);
        }
    }

    public void Reset()
    {
        _stack.Clear();
    }

    private static uint Fade(uint color, float alpha) => (color & 0x00_FF_FF_FF) | ((uint) (alpha * 255f) << 24);
}
