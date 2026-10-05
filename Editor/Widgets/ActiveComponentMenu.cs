using Editor.Managers;
using ImGuiNET;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Transforms;

namespace Editor.Widgets;

internal static class ActiveComponentMenu
{
    public static void Draw(SpatialComponent? component)
    {
        if (component?.Actor?.ActorManager is not { } manager) return;

        foreach (var system in manager.GetSystems<IActiveComponentSystem>())
        {
            if (!system.Accepts(component.GetType())) continue;

            var isCurrent = system.IsCurrent(component);
            if (ImGui.MenuItem($"{component.Icon}  {(isCurrent ? "Already" : "Make")} Active", enabled: !isCurrent)) system.Select(component);
            if (!isCurrent)
            {
                if (ImGui.MenuItem($"{component.Icon}  Select Active") && manager is InterfaceManager editor)
                    editor.SelectComponent(system.Current);
                ImGui.Separator();
            }
            return;
        }
    }
}
