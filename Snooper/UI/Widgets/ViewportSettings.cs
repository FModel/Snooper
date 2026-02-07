using System.Numerics;
using ImGuiNET;
using Snooper.Core.Hardware;
using Snooper.Rendering.Managers;

namespace Snooper.UI.Widgets;

internal class ViewportSettings(RendererInfo renderer) : IWidget<Viewport>
{
    public void Render(Viewport? context)
    {
        if (ImGui.Begin("Render Settings"))
        {
            if (context is null)
            {
                const string errorMessage = "No viewport selected";

                var textSize = ImGui.CalcTextSize(errorMessage);
                var windowSize = ImGui.GetWindowSize();

                ImGui.SetCursorPos(new Vector2((windowSize.X - textSize.X) * 0.5f, (windowSize.Y - textSize.Y) * 0.5f));
                ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), errorMessage);
            }
            else
            {
                ImGui.SetWindowFontScale(0.85f);
                ImGui.TextDisabled($"API: {renderer.Name} | GPU: {renderer.DeviceInfo.Name}");
                ImGui.SetWindowFontScale(1.0f);

                context.DrawControls();
            }
        }
        ImGui.End();
    }
}
