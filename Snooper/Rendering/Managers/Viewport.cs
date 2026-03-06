using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Rendering.Components.Camera;
using Snooper.UI;

namespace Snooper.Rendering.Managers;

public class Viewport(InteractiveCameraComponent camera) : IControllable, IResizable
{
    public InteractiveCameraComponent Camera { get; } = camera;

    public void DrawControls()
    {
        ImGui.SeparatorText("Camera");
        EditorUI.PropertyValueTable("Camera", () =>
        {
            var speed = Camera.MovementSpeed;
            var edited = EditorUI.DragFloat("Movement Speed", ref speed, 0.1f, 0.01f, 10000.0f);
            EditorUI.DragFloat("FOV", ref Camera.FieldOfView, 0.1f, 30.0f, 120.0f, "%.2f deg");

            var nearClip = Camera.NearClipPlane;
            var farClip = Camera.FarClipPlane;

            edited |= EditorUI.DragFloat("Near Clip Plane", ref nearClip, 0.1f, 0.01f, farClip - 0.1f);
            edited |= EditorUI.DragFloat("Far Clip Plane", ref farClip, 1.0f, nearClip + 0.1f, 100000.0f);

            if (edited)
            {
                Camera.MovementSpeed = speed;
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
