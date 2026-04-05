using System.Numerics;
using ImGuiNET;

namespace Snooper.UI;

public sealed class HeaderButtons(string headerLabel)
{
    private readonly record struct Entry(Func<string> Icon, Func<string> Tooltip, Action OnClick, Func<bool>? Enabled = null, Func<Vector4?>? TextColor = null);

    private readonly List<Entry> _entries = [];

    public HeaderButtons Add(string icon, string tooltip, Action onClick) => Add(() => icon, () => tooltip, onClick);
    public HeaderButtons Add(Func<string> icon, string tooltip, Action onClick) => Add(icon, () => tooltip, onClick);

    public HeaderButtons Add(Func<string> icon, Func<string> tooltip, Action onClick, Func<bool>? enabled = null, Func<Vector4?>? textColor = null)
    {
        _entries.Add(new Entry(icon, tooltip, onClick, enabled, textColor));
        return this;
    }

    public HeaderButtons Remove(string tooltip)
    {
        _entries.RemoveAll(e => e.Tooltip() == tooltip);
        return this;
    }

    public void Draw(Vector2 itemMin, Vector2 itemSize)
    {
        if (_entries.Count == 0) return;

        var style = ImGui.GetStyle();
        var btnSize = new Vector2(ImGui.GetFrameHeight());
        var padRight = style.FramePadding.X;

        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));

        for (var i = 0; i < _entries.Count; i++)
        {
            var fromRight = _entries.Count - 1 - i;
            var x = itemMin.X + itemSize.X - padRight - btnSize.X * (fromRight + 1);

            var entry = _entries[i];
            var isEnabled = entry.Enabled?.Invoke() ?? true;
            var tint = entry.TextColor?.Invoke();

            ImGui.SetCursorScreenPos(itemMin with { X = x });
            ImGui.BeginDisabled(!isEnabled);
            if (tint.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, tint.Value);
            if (ImGui.Button($"{entry.Icon()}##{headerLabel}_{i}", btnSize)) entry.OnClick();
            if (tint.HasValue) ImGui.PopStyleColor();
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(entry.Tooltip());
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
    }
}
