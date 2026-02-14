using ImGuiNET;

namespace Snooper.UI.Widgets;

internal class ImGuiDemo : IWidget
{
    public void Render()
    {
        ImGui.ShowDemoWindow();
    }
}
