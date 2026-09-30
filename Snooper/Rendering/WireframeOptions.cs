using System.Numerics;
using Snooper.UI;

namespace Snooper.Rendering;

public sealed class WireframeOptions : IControllable
{
    public bool Enabled;
    public Vector3 Color = new(0.5f, 0.5f, 0.5f);
    public float Width = 1f; // pixels
    public bool Overlay;

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Wireframe", () =>
        {
            EditorUI.Checkbox("Overlay", ref Overlay);
            EditorUI.ColorEdit3("Color", ref Color);
            EditorUI.SliderFloat("Width", ref Width, 1f, 4f, "%.1f px");
        });
    }
}
