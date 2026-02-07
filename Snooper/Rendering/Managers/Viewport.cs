using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;
using Snooper.UI;
using Snooper.UI.Widgets;

namespace Snooper.Rendering.Managers;

public class Viewport(InteractiveCameraComponent camera, RenderPipeline pipeline, GameWindow wnd) : IWidget, IControllable, IResizable
{
    public InteractiveCameraComponent Camera { get; } = camera;

    public void Render()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin($"Viewport##{Camera.Id}"))
        {
            var size = ImGui.GetContentRegionAvail();
            size.X -= ImGui.GetScrollX();
            size.Y -= ImGui.GetScrollY();

            Camera.Resize((int) size.X, (int) size.Y);
            ImGui.Image(pipeline.GetFinalTexture().GetPointer(), size, Vector2.UnitY, Vector2.UnitX);
            var itemMin = ImGui.GetItemRectMin();

            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    wnd.CursorState = CursorState.Grabbed;
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

    public void DrawControls()
    {
        ImGui.SeparatorText("Camera");
        EditorUI.PropertyValueTable("Camera", () =>
        {
            EditorUI.DragFloat("Speed", ref Camera.MovementSpeed, 0.1f, 1.0f, 1000.0f, "%.2f units/s");
            EditorUI.DragFloat("FOV", ref Camera.FieldOfView, 0.1f, 30.0f, 120.0f, "%.2f deg");

            var nearClip = Camera.NearClipPlane;
            var farClip = Camera.FarClipPlane;

            var edited = EditorUI.DragFloat("Near Clip Plane", ref nearClip, 0.1f, 0.01f, farClip - 0.1f);
            edited |= EditorUI.DragFloat("Far Clip Plane", ref farClip, 1.0f, nearClip + 0.1f, 100000.0f);

            if (edited)
            {
                Camera.NearClipPlane = nearClip;
                Camera.FarClipPlane = farClip;
            }
        });
    }

    public void Resize(int newWidth, int newHeight)
    {
        Camera.Resize(newWidth, newHeight);
    }
}
