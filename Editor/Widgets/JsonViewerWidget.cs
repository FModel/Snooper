using System.Numerics;
using ImGuiNET;

namespace Editor.Widgets;

public class JsonViewerWidget
{
    private bool _isOpen;
    private string _title = "JSON###JsonViewer";
    private string[] _layers = [];

    public void Open(string componentName, string[] jsonStrings)
    {
        _layers = jsonStrings;
        _title  = $"\uf1c9  {componentName}###JsonViewer";
        _isOpen = true;
    }

    public void Close() => _isOpen = false;
    public bool IsOpen  => _isOpen;

    public void Draw()
    {
        if (!_isOpen) return;

        ImGui.SetNextWindowSize(new Vector2(600, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_title, ref _isOpen, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
        {
            ImGui.End();
            return;
        }

        if (_layers.Length == 0)
        {
            ImGui.TextDisabled("No JSON data.");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##layers"))
        {
            for (var i = 0; i < _layers.Length; i++)
            {
                var label = i == 0 ? "\uf1c9  Component" : $"\uf0e8  Template {i}";
                if (ImGui.BeginTabItem($"{label}##t{i}"))
                {
                    DrawTextArea(_layers[i], i);
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawTextArea(string text, int id)
    {
        var style = ImGui.GetStyle();
        var avail = ImGui.GetContentRegionAvail();
        var btnSize = new Vector2(ImGui.GetFrameHeight());
        var origin = ImGui.GetCursorScreenPos();

        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.08f, 0.08f, 0.08f, 1f));
        ImGui.InputTextMultiline($"##json{id}", ref text, (uint)text.Length, new Vector2(avail.X, avail.Y), ImGuiInputTextFlags.ReadOnly);
        ImGui.PopStyleColor();

        var btnMin = new Vector2(origin.X + avail.X - style.ScrollbarSize - btnSize.X - style.FramePadding.X, origin.Y + style.FramePadding.Y);
        var btnMax = btnMin + btnSize;

        var mousePos = ImGui.GetIO().MousePos;
        var hovered = mousePos.X >= btnMin.X && mousePos.X <= btnMax.X && mousePos.Y >= btnMin.Y && mousePos.Y <= btnMax.Y;
        var clicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (clicked)
            ImGui.SetClipboardText(text);

        var dl = ImGui.GetForegroundDrawList();
        if (hovered)
        {
            dl.AddRectFilled(btnMin, btnMax,
                ImGui.IsMouseDown(ImGuiMouseButton.Left)
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.15f))
                    : ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.08f)),
                4f);
        }

        const string icon = "\uf0c5";
        var iconSize = ImGui.CalcTextSize(icon);
        var iconPos  = new Vector2(
            btnMin.X + (btnSize.X - iconSize.X) * 0.5f,
            btnMin.Y + (btnSize.Y - iconSize.Y) * 0.5f);

        dl.AddText(iconPos, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, hovered ? 1f : 0.4f)), icon);

        if (hovered)
        {
            ImGui.SetTooltip("Copy JSON");
        }
    }
}



