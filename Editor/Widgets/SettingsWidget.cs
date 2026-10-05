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

    private static readonly ActorSystemType[] _systemTypes = Enum.GetValues<ActorSystemType>();
    private static readonly string[] _systemTypeNames = Enum.GetNames<ActorSystemType>();

    protected override void DrawContents(EditorManager editor)
    {
        var lightSystem = editor.GetSystem<ClusteredLightSystem>();

        EditorUI.CollapsingTable("General", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Property("Fragment Color");
            EditorUI.FragmentColorCombo("##FragmentColor", ref editor.FragmentColor);

            ImGui.BeginDisabled(lightSystem is not { IsSupported: true });
            var sceneLights = lightSystem?.UseSceneLights ?? false;
            if (EditorUI.Checkbox("Scene Lights", ref sceneLights)) lightSystem!.UseSceneLights = sceneLights;
            ImGui.EndDisabled();
        });

        ImGui.BeginDisabled(!DeviceInfo.HasFragmentBarycentric);
        if (EditorUI.CollapsingToggle("Wireframe", ref editor.Wireframe.Enabled)) editor.Wireframe.DrawControls();
        ImGui.EndDisabled();

        editor.Pipeline.DrawControls();

        if (!ImGui.CollapsingHeader("Systems")) return;

        ImGui.Indent();
        for (var type = 0; type < _systemTypes.Length; type++)
        {
            var labelled = false;
            foreach (var system in editor.GetSystems<ActorSystem>())
            {
                if (system.SystemType != _systemTypes[type]) continue;

                if (!labelled)
                {
                    ImGui.SeparatorText(_systemTypeNames[type]);
                    labelled = true;
                }

                ImGui.PushID((int) system.Order);
                ImGui.BeginDisabled(!system.IsSupported);
                var enabled = system.IsEnabled;
                if (ImGui.Checkbox("##Enabled", ref enabled)) system.IsEnabled = enabled;
                ImGui.EndDisabled();
                ImGui.PopID();
                ImGui.SameLine();

                var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
                if (system is not IControllable) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                if (!ImGui.TreeNodeEx(system.DisplayName, flags) || system is not IControllable controllable) continue;

                controllable.DrawControls();
                ImGui.TreePop();
            }
        }
        ImGui.Unindent();
    }
}
