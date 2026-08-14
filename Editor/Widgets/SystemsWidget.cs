using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Systems;
using Snooper.UI;

namespace Editor.Widgets;

public class SystemsWidget : PanelWidget
{
    public override string PanelTitle => Settings.SystemsWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    protected override void DrawContents(EditorManager editor)
    {
        foreach (var system in editor.GetSystems<ActorSystem>())
        {
            var isBusy = system.DirtyComponentsCount > 0;
            if (isBusy)
            {
                var timeColor = ImGui.GetColorU32(new Vector4(0.8f, 0.5f, 0.0f, 0.5f + 0.5f * (float) Math.Sin(editor.Time * 5)));
                ImGui.PushStyleColor(ImGuiCol.Header, timeColor);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, timeColor);
            }

            if (ImGui.CollapsingHeader($"{system.Order}. {system.DisplayName}"))
            {
                ImGui.Columns(2, $"SysTable{system.Order}", false);
                {
                    var capacity = system.Capacity >= 0 ? $"/{system.Capacity:N0}" : "";
                    ImGui.TextDisabled("Components");
                    ImGui.TextUnformatted($"{system.ComponentsCount:N0}{capacity} {system.ComponentType.Name}{(system.ComponentsCount > 1 ? "s" : "")}");
                    ImGui.Spacing();
                    ImGui.TextDisabled("Dirty Components");
                    ImGui.TextUnformatted($"{system.DirtyComponentsCount:N0}{capacity} {system.ComponentType.Name}{(system.DirtyComponentsCount > 1 ? "s" : "")}");
                    ImGui.Spacing();
                    ImGui.TextDisabled("Max Binding Used");
                    ImGui.TextUnformatted($"{system.MaxBindingUsed?.ToString() ?? "N/A"}");
                    ImGui.NextColumn();
                    ImGui.TextDisabled("Show Wireframe");
                    ImGui.Checkbox($"##ShowWireframe{system.Order}", ref system.ShowWireframe);
                    ImGui.Spacing();
                    ImGui.TextDisabled("Is Enabled");
                    ImGui.Checkbox($"##Enabled{system.Order}", ref system.IsEnabled);
                }

                ImGui.Columns(1);
                if (system is IControllable controllable)
                {
                    if (ImGui.TreeNode($"Controls##SysControls{system.Order}"))
                    {
                        controllable.DrawControls();
                        ImGui.TreePop();
                    }
                }
            }

            if (isBusy)
            {
                ImGui.PopStyleColor(2);
            }
        }
    }
}
