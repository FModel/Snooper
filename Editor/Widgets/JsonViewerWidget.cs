using System.Numerics;
using ImGuiNET;
using Snooper.UI;

namespace Editor.Widgets;

public class JsonViewerWidget
{
    private readonly Dictionary<int, TreeNode> _openWindows = [];

    public void Open(TreeNode node)
    {
        _openWindows.TryAdd(node.Id, node);
    }

    public void DrawAll()
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

    private void DrawTextArea(string text, int id)
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.08f, 0.08f, 0.08f, 1f));
        ImGui.InputTextMultiline($"##json{id}", ref text, (uint)text.Length, new Vector2(avail.X, avail.Y), ImGuiInputTextFlags.ReadOnly);
        ImGui.PopStyleColor();
    }
}
