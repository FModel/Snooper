using System.Numerics;
using ImGuiNET;
using Snooper.Core.Hardware;

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
                ImGui.SeparatorText("Renderer");
                EditorUI.PropertyValueTable("Renderer", () =>
                {
                    EditorUI.Text("API", renderer.Name);
                    EditorUI.Text("GPU", renderer.DeviceInfo.Name);
                });

                ImGui.SeparatorText("Settings");
                context._frame.DrawControls();

                ImGui.SeparatorText("Advanced");
            }
        }
        ImGui.End();
    }
}
