using System.Numerics;
using ImGuiNET;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering;

namespace Snooper.UI;

public static class EditorUI
{
    public static bool FragmentColorCombo(string id, ref uint value) => LabelCombo(id, ref value, FragmentColorMode.Labels);

    public static bool LabelCombo(string id, ref uint value, string[] labels)
    {
        var preview = value < (uint)labels.Length ? labels[value] : "Unknown";
        var changed = false;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo(id, preview))
        {
            for (var i = 0u; i < labels.Length; i++)
            {
                if (ImGui.Selectable(labels[i], i == value))
                {
                    value = i;
                    changed = true;
                }
                if (i == value) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        return changed;
    }

    /// <summary>
    /// Draws a non-interactive colored square axis label and advances the cursor past it.
    /// </summary>
    private static void AxisLabel(uint color, float x)
    {
        var pos = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var size = new Vector2(x, ImGui.GetFrameHeight());

        dl.AddRectFilled(pos, pos + size, color);
        ImGui.Dummy(size);
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

    public static bool SliderInt(string label, ref int value, int min, int max, string? format = null)
    {
        Property(label);
        return ImGui.SliderInt("##" + label, ref value, min, max, format);
    }

    public static bool SliderFloat(string label, ref float value, float min, float max, string? format = null)
    {
        Property(label);
        return ImGui.SliderFloat("##" + label, ref value, min, max, format);
    }

    public static bool ColorEdit3(string label, ref Vector3 value, ImGuiColorEditFlags flags = ImGuiColorEditFlags.NoInputs)
    {
        Property(label);
        return ImGui.ColorEdit3("##" + label, ref value, flags);
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

    public static void Caption(params string[] lines)
    {
        if (lines.Length == 0) return;

        PushCaptionStyle();
        foreach (var line in lines) ImGui.TextUnformatted(line);
        PopCaptionStyle();
    }

    public static void PushCaptionStyle()
    {
        ImGui.SetWindowFontScale(0.85f);
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
    }

    public static void PopCaptionStyle()
    {
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1.0f);
    }

    public static void ListHeader(string label)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, ImGui.GetStyle().SeparatorTextPadding with { Y = 0f });
        ImGui.SeparatorText(label);
        ImGui.PopStyleVar();
    }

    public static void Property(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(-1);
    }

    public static void PushIconButtonStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));
    }

    public static void PopIconButtonStyle()
    {
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
    }

    public static void VerticalSeparator(float? paddingY = null)
    {
        var pad = paddingY ?? ImGui.GetStyle().FramePadding.Y;
        var size = new Vector2(1f, ImGui.GetFrameHeight());
        var pos = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddLine(
            pos with { Y = pos.Y + pad },
            pos with { Y = pos.Y + size.Y - pad },
            ImGui.GetColorU32(ImGuiCol.Separator), size.X);

        ImGui.Dummy(size);
    }

    public static bool IconButton(string icon, string? tooltip = null, bool enabled = true, Vector4? textColor = null)
    {
        PushIconButtonStyle();

        var height = ImGui.GetFrameHeight();
        var width = MathF.Max(height, ImGui.CalcTextSize(icon).X + ImGui.GetStyle().FramePadding.X * 2.0f);

        ImGui.BeginDisabled(!enabled);
        if (textColor.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, textColor.Value);
        var clicked = ImGui.Button(icon, new Vector2(width, height));
        if (textColor.HasValue) ImGui.PopStyleColor();
        ImGui.EndDisabled();

        PopIconButtonStyle();

        if (tooltip is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) Tooltip(tooltip);
        return clicked;
    }

    public static void PropertyWithToggle(string label, params PropertyToggleButton[] buttons)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        var visibleCount = 0;
        foreach (var button in buttons)
        {
            if (button.Visible?.Invoke() ?? true) visibleCount++;
        }

        var style = ImGui.GetStyle();
        var frameHeight = ImGui.GetFrameHeight();
        var startX = ImGui.GetCursorPosX();
        var colW = ImGui.GetContentRegionAvail().X;
        var labelEndX = startX + ImGui.CalcTextSize(label).X + style.ItemSpacing.X;
        var firstBtnX = startX + colW - frameHeight * visibleCount;

        PushIconButtonStyle();

        var slot = 0;
        for (var i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (!(btn.Visible?.Invoke() ?? true)) continue;

            var btnX = firstBtnX + frameHeight * slot++;
            if (btnX < labelEndX) continue; // not enough space – skip rather than overlap the label

            var isEnabled = btn.Enabled?.Invoke() ?? true;
            var tint = btn.TextColor?.Invoke();

            ImGui.SameLine(btnX);
            ImGui.BeginDisabled(!isEnabled);
            if (tint.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, tint.Value);
            if (ImGui.Button(btn.Icon() + "##Tog_" + label + "_" + i, new Vector2(frameHeight)))
                btn.OnClick();
            if (tint.HasValue) ImGui.PopStyleColor();
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && btn.Tooltip is not null)
            {
                Tooltip(btn.Tooltip.Invoke());
            }
        }

        PopIconButtonStyle();

        ImGui.TableSetColumnIndex(1);
        ImGui.SetNextItemWidth(-1);
    }

    public static void Tooltip(string text)
    {
        ImGui.BeginTooltip();
        var parts = text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        ImGui.TextUnformatted(parts[0]);
        if (parts.Length > 1)
        {
            ImGui.Spacing();
            Caption(parts[1..]);
        }
        ImGui.EndTooltip();
    }

    public static void DrawThumbnail(Texture? texture, string slotLabel, float size = 1.0f, int channel = -1)
    {
        var dimensions = new Vector2(ImGui.GetFrameHeight() * size);
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton($"##Thumb_{slotLabel}", dimensions);
        var drawList = ImGui.GetWindowDrawList();

        if (texture is null)
        {
            var labelSize = ImGui.CalcTextSize(slotLabel);
            drawList.AddText(origin + (dimensions - labelSize) * 0.5f, ImGui.GetColorU32(ImGuiCol.TextDisabled), slotLabel);
        }
        else using (ImGuiDrawCallbacks.Instance.IsolateChannel(drawList, channel))
        {
            drawList.AddImage(texture.GetPointer(), origin, origin + dimensions);
        }

        drawList.AddRect(origin, origin + dimensions, ImGui.GetColorU32(ImGuiCol.Border));

        if (ImGui.IsItemHovered())
        {
            Tooltip(texture is null ? $"{slotLabel}: none" : $"{slotLabel}: {texture.Name}\n{texture.Width}x{texture.Height}, {texture.FormatName}, {texture.GetFormattedSpace()}");
        }

        if (clicked && texture is not null)
        {
            WindowRequests.Request(Settings.TextureInspectorWindow, texture);
        }
    }

    public static void CollapsingTable(string label, ImGuiTreeNodeFlags flags, Action draws)
    {
        if (ImGui.CollapsingHeader(label, flags))
        {
            PropertyValueTable(label, draws);
        }
    }

    public static void PropertyValueTable(string label, Action draws, bool indent = true)
    {
        if (indent) ImGui.Indent();
        if (ImGui.BeginTable(label + "ControlsTable", 2))
        {
            ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 2.0f);

            draws.Invoke();

            ImGui.EndTable();
        }
        if (indent) ImGui.Unindent();
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

    private static bool DragAxesCore(string id, Span<float> values, ReadOnlySpan<uint> colors, bool linked, out int changedAxis, float speed, float min, float max, string format)
    {
        var n = values.Length;
        changedAxis = -1;
        var changed = false;

        var availW = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var labelW = MathF.Floor(spacing * 0.5f);
        var totalDrW = availW - labelW * n - spacing * (n - 1);
        var dragW = MathF.Floor(totalDrW / n);
        if (dragW < 1f) dragW = 1f;

        for (var i = 0; i < n; i++)
        {
            if (i > 0) ImGui.SameLine(0, spacing);
            AxisLabel(colors[i], labelW);
            ImGui.SameLine(0, 0);
            ImGui.SetNextItemWidth(i < n - 1 ? dragW : -1);
            if (ImGui.DragFloat($"##Ax{i}_{id}", ref values[i], speed, min, max, format))
            {
                changed = true;
                changedAxis = i;
            }
        }

        if (changed && linked && changedAxis >= 0)
        {
            values.Fill(values[changedAxis]);
        }

        return changed;
    }

    /// <summary>Renders per-axis X/Y/Z drag floats with coloured axis labels, filling the table cell exactly.</summary>
    public static bool DragAxes(string id, ref Vector3 value, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue, string? format = null)
    {
        Span<float> tmp = [value.X, value.Y, value.Z];
        Span<uint> colors = [Settings.AxisColorX, Settings.AxisColorY, Settings.AxisColorZ];
        var changed = DragAxesCore(id, tmp, colors, false, out _, speed, min, max, format ?? "%.3f");
        if (changed) value = new Vector3(tmp[0], tmp[1], tmp[2]);
        return changed;
    }

    /// <summary>
    /// Renders per-axis X/Y/Z drag floats with optional uniform-scale linking.
    /// When <paramref name="linked"/> is true, editing any axis sets all three to the same value.
    /// Returns true if any component changed; <paramref name="changedAxis"/> is 0/1/2 or -1.
    /// </summary>
    public static bool DragAxes(string id, ref Vector3 value, bool linked, out int changedAxis, float speed = 0.01f, float min = float.MinValue, float max = float.MaxValue)
    {
        Span<float> tmp = [value.X, value.Y, value.Z];
        Span<uint> colors = [Settings.AxisColorX, Settings.AxisColorY, Settings.AxisColorZ];
        var changed = DragAxesCore(id, tmp, colors, linked, out changedAxis, speed, min, max, "%.3f");
        if (changed) value = new Vector3(tmp[0], tmp[1], tmp[2]);
        return changed;
    }

    /// <summary>Renders per-component X/Y/Z/W drag floats for a quaternion, clamped to [-1, 1].</summary>
    public static bool DragAxes(string id, ref Quaternion value, float speed = 0.01f)
    {
        Span<float> tmp = [value.X, value.Y, value.Z, value.W];
        Span<uint> colors = [Settings.AxisColorX, Settings.AxisColorY, Settings.AxisColorZ, Settings.AxisColorW];
        var changed = DragAxesCore(id, tmp, colors, false, out _, speed, -1f, 1f, "%.3f");
        if (changed) value = new Quaternion(tmp[0], tmp[1], tmp[2], tmp[3]);
        return changed;
    }
}
