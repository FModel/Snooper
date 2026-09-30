using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Hardware;
using Snooper.Core.Systems;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Editor.Widgets;

public class SettingsWidget : PanelWidget
{
    public override string PanelTitle => Settings.SettingsWindow;
    public override PanelGroup Group => PanelGroup.Engine;

    protected override void DrawContents(EditorManager editor)
    {
        var lights = editor.GetSystem<ClusteredLightSystem>();

        EditorUI.CollapsingTable("General", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Property("Fragment Color");
            EditorUI.FragmentColorCombo("##FragmentColor", ref editor.FragmentColor);

            ImGui.BeginDisabled(lights == null);
            var sceneLights = lights?.UseSceneLights ?? false;
            if (EditorUI.Checkbox("Scene Lights", ref sceneLights)) lights!.UseSceneLights = sceneLights;
            ImGui.EndDisabled();
        });

        ImGui.BeginDisabled(!DeviceInfo.HasFragmentBarycentric);
        if (EditorUI.CollapsingToggle("Wireframe", ref editor.Wireframe.Enabled)) editor.Wireframe.DrawControls();
        ImGui.EndDisabled();

        editor.Pipeline.DrawControls();

        if (!ImGui.CollapsingHeader("Systems")) return;

        ImGui.Indent();
        foreach (var system in editor.GetSystems<ActorSystem>())
        {
            ImGui.PushID((int) system.Order);
            ImGui.Checkbox("##Enabled", ref system.IsEnabled);
            ImGui.PopID();
            ImGui.SameLine();

            var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
            if (system is not IControllable) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (!ImGui.TreeNodeEx(system.DisplayName, flags) || system is not IControllable controllable) continue;

            controllable.DrawControls();
            ImGui.TreePop();
        }
        ImGui.Unindent();
    }
}
