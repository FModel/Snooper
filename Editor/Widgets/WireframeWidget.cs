using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Hardware;
using Snooper.Core.Systems;
using Snooper.UI;

namespace Editor.Widgets;

public class WireframeWidget : PanelWidget
{
    public override string PanelTitle => Settings.WireframeWindow;
    public override PanelGroup Group => PanelGroup.Tools;

    public override bool IsOpen { get; set; }

    protected override void DrawContents(EditorManager editor)
    {
        if (!DeviceInfo.HasFragmentBarycentric)
        {
            ImGui.TextColored(Settings.OrangeColor, "The device has no GL_NV_fragment_shader_barycentric, there is no wireframe without it.");
            return;
        }

        var options = editor.Wireframe;
        EditorUI.PropertyValueTable("Wireframe", () =>
        {
            EditorUI.Checkbox("Enabled", ref options.Enabled);
            EditorUI.ColorEdit3("Color", ref options.Color);
            EditorUI.SliderFloat("Width (px)", ref options.Width, 1f, 4f, "%.1f");
            EditorUI.Checkbox("Overlay", ref options.Overlay);
        }, false);

        // one system at a time, when not all of them
        EditorUI.ListHeader("Systems");
        ImGui.BeginDisabled(options.Enabled);
        foreach (var system in editor.GetSystems<ActorSystem>())
        {
            if (system is not IGeometryRenderSystem) continue;

            ImGui.Checkbox($"{system.DisplayName}##Wireframe{system.Order}", ref system.ShowWireframe);
        }
        ImGui.EndDisabled();
    }
}
