using ImGuiNET;

namespace Snooper.UI.Containers.Buffers;

public readonly struct ImGuiMeasuredText(string text)
{
    public readonly string Text = text;
    public readonly float Width = ImGui.CalcTextSize(text).X;
}