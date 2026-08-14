using System.Globalization;
using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

public class MorphTargetWidget : PanelWidget
{
    public override string PanelTitle => Settings.MorphTargetsWindow;
    public override PanelGroup Group => PanelGroup.Tools;

    public override bool IsOpen { get; set; } // this widget is opened on demand

    private int _lastComponentId = -1;
    private string _search = "";
    private bool _activeOnly;
    private bool _dirty = true;

    private readonly List<int> _filtered = []; // morph indices surviving the name search
    private readonly List<int> _visible = []; // and the active-only pass over those

    protected override void DrawContents(EditorManager editor)
    {
        if ((editor.SelectedComponent ?? editor.SelectedActor?.RootComponent) is not SkinnedMeshComponent mesh)
        {
            ImGui.TextDisabled("No skinned mesh selected.");
            return;
        }

        if (mesh.Descriptor.Morphs is not { Count: > 0 } morphs)
        {
            ImGui.TextDisabled($"{mesh.Descriptor.Name ?? "This mesh"} has no morph targets.");
            return;
        }

        if (mesh.Id != _lastComponentId)
        {
            _lastComponentId = mesh.Id;
            _dirty = true;
        }

        var weights = mesh.MorphWeights;
        DrawToolbar(mesh, weights);

        if (_dirty)
        {
            _filtered.Clear();
            var isSearching = !string.IsNullOrWhiteSpace(_search);
            for (var i = 0; i < morphs.Count; i++)
            {
                if (isSearching && !morphs.Names[i].Contains(_search, StringComparison.OrdinalIgnoreCase)) continue;
                _filtered.Add(i);
            }
            _dirty = false;
        }

        var rows = _filtered;
        if (_activeOnly)
        {
            // unlike the name match this one cannot be cached, the weights move under it every frame,
            // but it is a float compare over an already filtered list
            _visible.Clear();
            foreach (var index in _filtered)
            {
                if (index < weights.Length && weights[index] != 0.0f) _visible.Add(index);
            }
            rows = _visible;
        }

        ImGui.TextDisabled($"{rows.Count} / {morphs.Count} morph{(morphs.Count != 1 ? "s" : "")}");
        DrawList(mesh, morphs, weights, rows);
    }

    private void DrawToolbar(SkinnedMeshComponent mesh, float[] weights)
    {
        var style = ImGui.GetStyle();
        var iconWidth = ImGui.CalcTextSize(Settings.MagnifyingGlassIcon).X;
        var activeWidth = ImGui.CalcTextSize("Active only").X + ImGui.GetFrameHeight() + style.ItemInnerSpacing.X;
        var resetWidth = ImGui.CalcTextSize($"{Settings.LoopIcon}  Reset All").X + style.FramePadding.X * 2;
        var inputWidth = MathF.Max(ImGui.GetContentRegionAvail().X - iconWidth - activeWidth - resetWidth - style.ItemSpacing.X * 3, ImGui.GetFrameHeight() * 3);

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(Settings.MagnifyingGlassIcon);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputTextWithHint("##MorphSearch", "Search...", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll))
        {
            _dirty = true;
        }

        ImGui.SameLine();
        ImGui.Checkbox("Active only", ref _activeOnly);

        var anyActive = false;
        foreach (var weight in weights)
        {
            if (weight == 0.0f) continue;
            anyActive = true;
            break;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!anyActive);
        if (ImGui.Button($"{Settings.LoopIcon}  Reset All"))
        {
            Array.Clear(weights);
            mesh.MarkDirty(DirtyFlags.Morph);
        }
        ImGui.EndDisabled();
    }

    private static void DrawList(SkinnedMeshComponent mesh, MorphDescriptor morphs, float[] weights, List<int> rows)
    {
        if (rows.Count == 0)
        {
            ImGui.TextDisabled("No morph target matches.");
            return;
        }

        if (ImGui.BeginChild("##MorphList", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            var drawList = ImGui.GetWindowDrawList();
            var style = ImGui.GetStyle();

            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(rows.Count, ImGui.GetFrameHeightWithSpacing());
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        DrawRow(mesh, morphs.Names[rows[i]], weights, rows[i], drawList, style);
                    }
                }
                clipper.End();
            }
        }
        ImGui.EndChild();
    }

    private static void DrawRow(SkinnedMeshComponent mesh, string name, float[] weights, int index, ImDrawListPtr drawList, ImGuiStylePtr style)
    {
        ImGui.SetNextItemWidth(-1);

        if (ImGui.SliderFloat($"##MorphWeight{index}", ref weights[index], 0.0f, 1.0f, ""))
        {
            mesh.MarkDirty(DirtyFlags.Morph);
        }

        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        var weight = weights[index];
        var value = weight.ToString("0.00", CultureInfo.InvariantCulture);
        var valueWidth = ImGui.CalcTextSize(value).X;
        var textY = min.Y + style.FramePadding.Y;
        var nameWidth = max.X - style.FramePadding.X * 2.0f - valueWidth - style.ItemInnerSpacing.X - min.X;

        drawList.AddText(new Vector2(max.X - style.FramePadding.X - valueWidth, textY), ImGui.GetColorU32(weight != 0.0f ? ImGuiCol.Text : ImGuiCol.TextDisabled), value);

        drawList.PushClipRect(min, max with { X = min.X + style.FramePadding.X + nameWidth }, true);
        drawList.AddText(new Vector2(min.X + style.FramePadding.X, textY), ImGui.GetColorU32(ImGuiCol.Text), name);
        drawList.PopClipRect();

        if (hovered && !active && ImGui.CalcTextSize(name).X > nameWidth)
        {
            ImGui.SetTooltip(name);
        }
    }
}
