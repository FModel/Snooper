using System.Numerics;
using ImGuiNET;

namespace Snooper.UI;

public static class EditorUI
{
    // ── Axis label colors ────────────────────────────────────────────────────
    private static readonly Vector4 _axisColorX = new(0.5f, 0.000f, 0.000f, 1f);
    private static readonly Vector4 _axisColorY = new(0.000f, 0.5f, 0.000f, 1f);
    private static readonly Vector4 _axisColorZ = new(0.000f, 0.000f, 0.5f, 1f);
    private static readonly Vector4 _axisColorW = new(0.25f, 0.000f, 0.5f, 1f);

    private static uint ToU32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    /// <summary>
    /// Draws a non-interactive colored square axis label and advances the cursor past it.
    /// </summary>
    private static void AxisLabel(string letter, Vector4 color, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl  = ImGui.GetWindowDrawList();

        dl.AddRectFilled(pos, pos + new Vector2(size), ToU32(color));

        // Center the letter inside the tile
        var textSize = ImGui.CalcTextSize(letter);
        var textPos  = pos + (new Vector2(size) - textSize) * 0.5f;
        dl.AddText(textPos, 0xFFFFFFFF, letter);

        // Advance cursor by the tile size so the next SameLine lands correctly
        ImGui.Dummy(new Vector2(size));
    }

    public static void CenteredText(string text, Vector4? color = null)
    {
        var textSize = ImGui.CalcTextSize(text);
        var windowSize = ImGui.GetWindowSize();

        ImGui.SetCursorPos(new Vector2((windowSize.X - textSize.X) * 0.5f, (windowSize.Y - textSize.Y) * 0.5f));
        if (color == null)
        {
            ImGui.TextUnformatted(text);
        }
        else
        {
            ImGui.TextColored(color.Value, text);
        }
    }

    public static void CenteredErrorText(string text)
    {
        CenteredText(text, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
    }

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

    public static void TogglableTreeNode(string label, ref bool enabled, Action? content = null, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.SpanAvailWidth)
    {
        var local = enabled;
        TogglableTreeNode(label, local, content, toggle => local = toggle, flags);
        enabled = local;
    }

    public static void TogglableTreeNode(string label, bool enabled = false, Action? content = null, Action<bool>? onToggle = null, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.SpanAvailWidth)
    {
        flags |= ImGuiTreeNodeFlags.AllowOverlap;
        if (content is null) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet;
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
        {
            enabled = !enabled;
            onToggle?.Invoke(enabled);
        }

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
            content?.Invoke();
            ImGui.TreePop();
        }
    }

    /// <summary>
    /// Renders a compact square toggle button that is DPI-aware (sized to frame height).
    /// </summary>
    public static bool ToggleButtonSquare(string id, string label, ref bool value, Vector4? activeColor = null)
    {
        var size = new Vector2(ImGui.GetFrameHeight());

        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);

        if (value)
        {
            var col = activeColor.HasValue
                ? ImGui.ColorConvertFloat4ToU32(activeColor.Value)
                : ImGui.GetColorU32(ImGuiCol.ButtonActive);
            ImGui.PushStyleColor(ImGuiCol.Button, col);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, col);
        }

        var changed = ImGui.Button(label + "##" + id, size);

        if (value) ImGui.PopStyleColor(2);
        value = changed ? !value : value;

        ImGui.PopStyleVar(2);
        return changed;
    }

    /// <summary>
    /// Renders per-axis X/Y/Z drag floats with coloured axis labels inside the current table value column.
    /// Returns true if any component changed.
    /// </summary>
    public static bool DragFloat3Axes(string id, ref Vector3 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        var changed = false;
        var availW  = ImGui.GetContentRegionAvail().X;
        var labelW  = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var dragW   = (availW - (labelW + spacing) * 3) / 3.0f;
        if (dragW < 1f) dragW = 1f;

        // X
        AxisLabel("X", _axisColorX, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##X_" + id, ref value.X, speed, min, max, format ?? "%.3f")) changed = true;

        ImGui.SameLine(0, spacing);

        // Y
        AxisLabel("Y", _axisColorY, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##Y_" + id, ref value.Y, speed, min, max, format ?? "%.3f")) changed = true;

        ImGui.SameLine(0, spacing);

        // Z
        AxisLabel("Z", _axisColorZ, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##Z_" + id, ref value.Z, speed, min, max, format ?? "%.3f")) changed = true;

        return changed;
    }

    /// <summary>
    /// Renders per-axis X/Y/Z drag floats with scale-link support.
    /// When linked, editing one axis uniformly scales all axes proportionally.
    /// Returns true if any component changed, and sets changedAxis to the index that changed (0/1/2 or -1).
    /// </summary>
    public static bool DragFloat3AxesLinked(string id, ref Vector3 value, bool linked, out int changedAxis,
        float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        changedAxis = -1;
        var changed = false;
        var availW  = ImGui.GetContentRegionAvail().X;
        var labelW  = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var dragW   = (availW - (labelW + spacing) * 3) / 3.0f;
        if (dragW < 1f) dragW = 1f;

        // X
        AxisLabel("X", _axisColorX, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##X_" + id, ref value.X, speed, min, max, "%.3f")) { changed = true; changedAxis = 0; }

        ImGui.SameLine(0, spacing);

        // Y
        AxisLabel("Y", _axisColorY, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##Y_" + id, ref value.Y, speed, min, max, "%.3f")) { changed = true; changedAxis = 1; }

        ImGui.SameLine(0, spacing);

        // Z
        AxisLabel("Z", _axisColorZ, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##Z_" + id, ref value.Z, speed, min, max, "%.3f")) { changed = true; changedAxis = 2; }

        if (changed && linked && changedAxis >= 0)
        {
            var newVal = changedAxis switch { 0 => value.X, 1 => value.Y, _ => value.Z };
            value = new Vector3(newVal);
        }

        return changed;
    }

    /// <summary>
    /// Renders per-axis X/Y/Z/W drag floats for a Quaternion with coloured axis labels.
    /// </summary>
    public static bool DragFloat4Axes(string id, ref Quaternion value, float speed = 0.01f)
    {
        var changed = false;
        var availW  = ImGui.GetContentRegionAvail().X;
        var labelW  = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var dragW   = (availW - (labelW + spacing) * 4) / 4.0f;
        if (dragW < 1f) dragW = 1f;

        // X
        AxisLabel("X", _axisColorX, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##QX_" + id, ref value.X, speed, -1f, 1f, "%.3f")) changed = true;

        ImGui.SameLine(0, spacing);

        // Y
        AxisLabel("Y", _axisColorY, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##QY_" + id, ref value.Y, speed, -1f, 1f, "%.3f")) changed = true;

        ImGui.SameLine(0, spacing);

        // Z
        AxisLabel("Z", _axisColorZ, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##QZ_" + id, ref value.Z, speed, -1f, 1f, "%.3f")) changed = true;

        ImGui.SameLine(0, spacing);

        // W
        AxisLabel("W", _axisColorW, labelW);
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(dragW);
        if (ImGui.DragFloat("##QW_" + id, ref value.W, speed, -1f, 1f, "%.3f")) changed = true;

        return changed;
    }
}
