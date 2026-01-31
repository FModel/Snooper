using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Containers;

namespace Snooper.UI.Widgets;

internal class Viewport(GameWindow wnd, string name, CameraFramePair frame) : IWidget
{
    internal readonly GameWindow _wnd = wnd;
    internal readonly CameraFramePair _frame = frame;

    public void Render()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin(name))
        {
            var size = ImGui.GetContentRegionAvail();
            size.X -= ImGui.GetScrollX();
            size.Y -= ImGui.GetScrollY();

            _frame.Camera.Resize((int) size.X, (int) size.Y);
            var textures = _frame.GetTextures();

            ImGui.Image(textures[^1].GetPointer(), size, Vector2.UnitY, Vector2.UnitX);
            var itemMin = ImGui.GetItemRectMin();

            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    _wnd.CursorState = CursorState.Grabbed;
                }
            }

            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold]);

            const float margin = 7.5f;
            var frameHeight = ImGui.GetFrameHeight();
            var drawList = ImGui.GetWindowDrawList();

            var framerate = ImGui.GetIO().Framerate;
            drawList.AddText(
                new Vector2(itemMin.X + margin, itemMin.Y + size.Y - frameHeight),
                ImGui.GetColorU32(ImGuiCol.Text),
                $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms) ({size.X} x {size.Y} px)"
            );

            const string label = "Previewed content may differ from final version saved or used in-game.";
            drawList.AddText(
                new Vector2(itemMin.X + size.X - ImGui.CalcTextSize(label).X - margin, itemMin.Y + size.Y - frameHeight),
                ImGui.GetColorU32(new Vector4(1.00f, 1.00f, 1.00f, 0.50f)),
                label
            );

            ImGui.PopFont();
        }
        ImGui.PopStyleVar();
        ImGui.End();
    }
}
