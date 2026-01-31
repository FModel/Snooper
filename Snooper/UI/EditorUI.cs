using System.Numerics;
using ImGuiNET;

namespace Snooper.UI;

public static class EditorUI
{
    public static bool DragFloat3(string label, ref Vector3 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        Property(label);
        return ImGui.DragFloat3("##" + label, ref value, speed, min, max, format);
    }

    public static bool DragFloat4(string label, ref Quaternion value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        var vec = new Vector4(value.X, value.Y, value.Z, value.W);
        var changed = DragFloat4(label, ref vec, speed, min, max, format);
        if (changed)
            value = new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
        return changed;
    }

    public static bool DragFloat4(string label, ref Vector4 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        Property(label);
        return ImGui.DragFloat4("##" + label, ref value, speed, min, max, format);
    }

    public static bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        Property(label);
        return ImGui.DragFloat("##" + label, ref value, speed, min, max, format);
    }

    public static bool Checkbox(string label, ref bool value)
    {
        Property(label);
        return ImGui.Checkbox("##" + label, ref value);
    }

    public static void Text(string label, string value)
    {
        Property(label);
        ImGui.TextUnformatted(value);
    }

    public static void Property(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
    }

    public static void CollapsingTable(string label, ImGuiTreeNodeFlags flags, Action draws)
    {
        if (ImGui.CollapsingHeader(label, flags))
        {
            PropertyValueTable(label, draws);
        }
    }

    public static void PropertyValueTable(string label, Action draws)
    {
        ImGui.Indent();
        if (ImGui.BeginTable(label + "ControlsTable", 2))
        {
            ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 2.0f);

            draws.Invoke();

            ImGui.EndTable();
        }
        ImGui.Unindent();
    }

    /// <summary>
    /// Creates a tree node with shared state.
    /// Temporarily pops the current ID scope so the tree node state is global,
    /// then pushes it back for the content inside.
    /// </summary>
    public static bool SharedTreeNode(string label, ImGuiTreeNodeFlags flags, uint id, Action content)
    {
        ImGui.PopID();
        var isOpen = ImGui.TreeNodeEx(label, flags);

        if (isOpen)
        {
            ImGui.PushID((int)id);
            content.Invoke();
            ImGui.PopID();
            ImGui.TreePop();
        }

        ImGui.PushID((int)id);
        return isOpen;
    }

    public static void TogglableTreeNode(string label, ref bool enabled, ImGuiTreeNodeFlags flags, Action content)
    {
        flags |= ImGuiTreeNodeFlags.AllowOverlap;
        var nodeOpen = ImGui.TreeNodeEx("##Tree_" + label, flags);
        ImGui.SameLine();

        var cursorY = ImGui.GetCursorPosY();
        var fontSize = ImGui.GetFontSize();
        var frameH = ImGui.GetFrameHeight();
        var checkboxSize = Math.Clamp(fontSize * 0.9f, 8.0f, frameH * 0.72f);

        var yAdjust = (fontSize - checkboxSize) / 2.0f;
        if (!float.IsNaN(yAdjust) && MathF.Abs(yAdjust) > 0.001f)
            ImGui.SetCursorPosY(cursorY + yAdjust);

        if (ImGui.InvisibleButton("##Toggle_" + label, new Vector2(checkboxSize, checkboxSize)))
            enabled = !enabled;

        var hovered = ImGui.IsItemHovered();
        var bbMin = ImGui.GetItemRectMin();
        var bbMax = ImGui.GetItemRectMax();

        const float rounding = 2.0f;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(bbMin, bbMax, ImGui.GetColorU32(hovered ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg), rounding);
        dl.AddRect(bbMin, bbMax, ImGui.GetColorU32(hovered ? ImGuiCol.HeaderHovered : ImGuiCol.Border), rounding);

        if (enabled)
        {
            var innerPad = checkboxSize * 0.18f;
            var innerMin = new Vector2(bbMin.X + innerPad, bbMin.Y + innerPad);
            var innerMax = new Vector2(bbMax.X - innerPad, bbMax.Y - innerPad);

            var innerW = innerMax.X - innerMin.X;
            var innerH = innerMax.Y - innerMin.Y;

            var n0 = new Vector2(0.0f, 0.55f);
            var n1 = new Vector2(0.45f, 1.0f);
            var n2 = new Vector2(1.0f, 0.0f);
            var p0 = new Vector2(innerMin.X + n0.X * innerW, innerMin.Y + n0.Y * innerH);
            var p1 = new Vector2(innerMin.X + n1.X * innerW, innerMin.Y + n1.Y * innerH);
            var p2 = new Vector2(innerMin.X + n2.X * innerW, innerMin.Y + n2.Y * innerH);

            var halfPixel = new Vector2(0.5f);
            p0 -= halfPixel;
            p1 -= halfPixel;
            p2 -= halfPixel;

            var thickness = MathF.Max(1.0f, fontSize * 0.11f);
            var colCheck = ImGui.GetColorU32(ImGuiCol.CheckMark);

            dl.AddLine(p0, p1, colCheck, thickness);
            dl.AddLine(p1, p2, colCheck, thickness);
        }

        ImGui.SetCursorPosY(cursorY);
        ImGui.SameLine();
        ImGui.TextUnformatted(label);

        if (nodeOpen)
        {
            content.Invoke();
            ImGui.TreePop();
        }
    }
}
