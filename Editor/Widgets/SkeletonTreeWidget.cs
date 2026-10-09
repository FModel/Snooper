using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;

namespace Editor.Widgets;

public class SkeletonTreeWidget : PanelWidget
{
    public override string PanelTitle => Settings.SkeletonWindow;
    public override PanelGroup Group => PanelGroup.Tools;

    public override bool IsOpen { get; set; } // this widget is opened on demand

    private readonly record struct Row(int Bone, int Socket, int Depth, bool HasChildren);

    private int _lastComponentId = -1;
    private string _search = "";
    private bool _dirty = true;
    private (int Bone, int Socket) _selection = (-1, -1);

    private readonly List<Row> _rows = []; // the tree flattened to what is unfolded, so it can be clipped
    private readonly HashSet<int> _collapsed = [];

    internal static bool ShowSockets { get; private set; }

    protected override void DrawContents(EditorManager editor)
    {
        if ((editor.SelectedComponent ?? editor.SelectedActor?.RootComponent) is not SkinnedMeshComponent mesh)
        {
            ImGui.TextDisabled("No skinned mesh selected.");
            return;
        }

        var pose = mesh.Pose;
        var skeleton = pose.Skeleton;
        var style = ImGui.GetStyle();
        var sockets = mesh.Descriptor.Sockets;

        if (mesh.Id != _lastComponentId)
        {
            _lastComponentId = mesh.Id;
            _collapsed.Clear();
            _dirty = true;
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - (ImGui.GetFrameHeight() + style.ItemInnerSpacing.X) * 4f);
        if (ImGui.InputTextWithHint("##BoneFilter", $"{Settings.MagnifyingGlassIcon}  Filter", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EscapeClearsAll))
        {
            _dirty = true;
        }

        if (Button(Settings.UpRightAndDownLeftFromCenterIcon, "Expand All"))
        {
            _collapsed.Clear();
            _dirty = true;
        }

        if (Button(Settings.DownLeftAndUpRightToCenterIcon, "Collapse All"))
        {
            for (var i = 0; i < skeleton.BoneCount; i++)
            {
                _collapsed.Add(i);
            }
            _dirty = true;
        }

        if (Button(Settings.PlugIcon, ShowSockets ? "Hide Sockets" : "Show Sockets", ShowSockets))
        {
            ShowSockets = !ShowSockets;
            _dirty = true;
        }

        if (Button(Settings.ArrowRotateLeftIcon, "Reset All Bones")) mesh.ResetBones();

        // a selection made elsewhere is revealed, one made in the tree is already on screen
        var reveal = _selection != (mesh.SelectedBone, mesh.SelectedSocket);
        if (reveal) Unfold();
        if (_dirty) Rebuild();

        if (ImGui.BeginChild("##BoneTree", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            var height = ImGui.GetTextLineHeightWithSpacing();
            for (var i = 0; reveal && i < _rows.Count; i++)
            {
                if (_rows[i].Bone != mesh.SelectedBone || _rows[i].Socket != mesh.SelectedSocket) continue;

                ImGui.SetScrollFromPosY(ImGui.GetCursorStartPos().Y + (i + 0.5f) * height, 0.5f);
                break;
            }

            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_rows.Count, height);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        DrawRow(_rows[i]);
                    }
                }
                clipper.End();
                clipper.Destroy();
            }
        }
        ImGui.EndChild();

        _selection = (mesh.SelectedBone, mesh.SelectedSocket);

        bool Button(string icon, string tooltip, bool active = true)
        {
            ImGui.SameLine(0, style.ItemInnerSpacing.X);
            if (!active) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            var clicked = ImGui.Button(icon, new Vector2(ImGui.GetFrameHeight()));
            if (!active) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) EditorUI.Tooltip(tooltip);

            return clicked;
        }

        void Unfold()
        {
            var bone = mesh.SelectedBone;
            if (mesh.SelectedSocket >= 0 && sockets[mesh.SelectedSocket] is SkeletalMeshSocketDescriptor socket && skeleton.BoneNameToIndex.TryGetValue(socket.BoneName, out var owner))
            {
                bone = (int) owner;
                _dirty |= _collapsed.Remove(bone);
            }

            while (bone >= 0 && (bone = skeleton.GetBoneParentIndex(bone)) >= 0)
            {
                _dirty |= _collapsed.Remove(bone);
            }
        }

        void Rebuild()
        {
            _rows.Clear();
            _dirty = false;

            var filter = _search.Trim();
            if (filter.Length == 0)
            {
                for (var i = 0; i < skeleton.BoneCount; i++)
                {
                    if (skeleton.BoneDescriptors[i].IsRoot) Add(i, 0);
                }

                return;
            }

            for (var i = 0; i < skeleton.BoneCount; i++)
            {
                if (skeleton.GetBoneName(i).Contains(filter, StringComparison.OrdinalIgnoreCase)) _rows.Add(new Row(i, -1, 0, false));
            }

            for (var i = 0; ShowSockets && i < sockets.Length; i++)
            {
                if (sockets[i] is { } socket && socket.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) _rows.Add(new Row(-1, i, 0, false));
            }

            void Add(int bone, int depth)
            {
                var name = skeleton.GetBoneName(bone);
                var children = skeleton.GetBoneChildren(bone);

                var row = _rows.Count;
                _rows.Add(new Row(bone, -1, depth, children.Count > 0));

                for (var i = 0; ShowSockets && i < sockets.Length; i++)
                {
                    if (sockets[i] is not SkeletalMeshSocketDescriptor socket || !socket.BoneName.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

                    _rows[row] = _rows[row] with { HasChildren = true };
                    if (!_collapsed.Contains(bone)) _rows.Add(new Row(-1, i, depth + 1, false));
                }

                if (_collapsed.Contains(bone)) return;
                foreach (var child in children)
                {
                    Add(child, depth + 1);
                }
            }
        }

        void DrawRow(Row row)
        {
            var flags = ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
            if (!row.HasChildren) flags |= ImGuiTreeNodeFlags.Leaf;
            if (row.Bone == mesh.SelectedBone && row.Socket == mesh.SelectedSocket) flags |= ImGuiTreeNodeFlags.Selected;

            if (row.Depth > 0) ImGui.Indent(row.Depth * style.IndentSpacing);
            if (row.Socket >= 0)
            {
                ImGui.TreeNodeEx($"{Settings.PlugIcon} {sockets[row.Socket]?.Name}", flags);
                if (ImGui.IsItemClicked()) mesh.SelectedSocket = row.Socket == mesh.SelectedSocket ? -1 : row.Socket;
            }
            else
            {
                var edited = pose.IsBoneEdited(row.Bone);
                if (edited) ImGui.PushStyleColor(ImGuiCol.Text, Settings.OrangeColor);
                ImGui.SetNextItemOpen(!_collapsed.Contains(row.Bone));
                ImGui.TreeNodeEx(skeleton.GetBoneName(row.Bone), flags);
                if (edited) ImGui.PopStyleColor();

                if (ImGui.IsItemToggledOpen())
                {
                    if (!_collapsed.Remove(row.Bone)) _collapsed.Add(row.Bone);
                    _dirty = true;
                }
                else if (ImGui.IsItemClicked())
                {
                    mesh.SelectedBone = row.Bone == mesh.SelectedBone ? -1 : row.Bone;
                }

                if (ImGui.BeginPopupContextItem())
                {
                    DrawBoneMenu(row.Bone);
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                ImGui.TextDisabled($"{row.Bone}");
            }
            if (row.Depth > 0) ImGui.Unindent(row.Depth * style.IndentSpacing);
        }

        void DrawBoneMenu(int bone)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            ImGui.TextUnformatted($"[{bone}] {mesh.Descriptor.Skeleton?.GetBoneName(bone)}");
            ImGui.PopStyleColor();
            ImGui.Separator();

            if (ImGui.MenuItem($"{Settings.ArrowRotateLeftIcon}  Reset Bone")) mesh.ResetBone(bone);
            if (ImGui.MenuItem($"{Settings.ArrowRotateLeftIcon}  Reset All Bones")) mesh.ResetBones();
        }
    }
}
