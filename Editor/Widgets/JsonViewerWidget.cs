using System.Numerics;
using ImGuiNET;
using Snooper.UI;

namespace Editor.Widgets;

public static class JsonViewerWidget
{
    private static readonly Dictionary<int, TreeNode> _openWindows = [];

    public static void Open(TreeNode node)
    {
        _openWindows.TryAdd(node.Id, node);
    }

    public static void DrawAll()
    {
        if (_openWindows.Count == 0) return;

        var toClose = new List<int>();

        foreach (var (id, node) in _openWindows)
        {
            var open = true;
            ImGui.SetNextWindowSize(new Vector2(600, 560), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin($"\uf1c9  {node.Name}##JsonViewer{id}", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                ImGui.End();
                if (!open) toClose.Add(id);
                continue;
            }

            if (node.JsonProperties == null || node.JsonProperties.Length == 0)
            {
                ImGui.TextDisabled("No JSON data.");
            }
            else if (ImGui.BeginTabBar($"##layers{id}"))
            {
                for (var i = 0; i < node.JsonProperties.Length; i++)
                {
                    var label = i == 0 ? "\uf1c9  Object" : $"\uf0e8  Template {i}";
                    if (ImGui.BeginTabItem($"{label}##t{i}"))
                    {
                        DrawTextArea(node.JsonProperties[i], id * 1000 + i);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.End();
            if (!open) toClose.Add(id);
        }

        foreach (var id in toClose)
            _openWindows.Remove(id);
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
