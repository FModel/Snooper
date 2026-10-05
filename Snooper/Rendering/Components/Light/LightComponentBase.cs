using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using ImGuiNET;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

public abstract class LightComponentBase : BillboardComponent
{
    public float Intensity;
    public Vector3 Color;
    public readonly bool CastShadows;

    private readonly float _step;

    protected LightComponentBase(ULightComponentBase component, string sprite) : base(component, sprite)
    {
        Intensity = component.Intensity;
        Color = component.GetLightColor();
        CastShadows = component.CastShadows;

        _step = MathF.Max(Intensity * 0.01f, 0.01f);
    }

    protected LightComponentBase(float intensity, Vector3 color, bool castShadows, string sprite, Transform? transform = null, string? name = null) : base(sprite, transform, name)
    {
        Intensity = intensity;
        Color = color;
        CastShadows = castShadows;

        _step = MathF.Max(Intensity * 0.01f, 0.01f);
    }

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Light", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Cast Shadows", CastShadows ? "Yes" : "No");
            DrawLightControls();
        });
    }

    protected virtual bool DrawLightControls()
    {
        EditorUI.Property("Intensity");
        var edited = ImGui.DragFloat("##Intensity", ref Intensity, _step, 0.0f, float.MaxValue);

        edited |= EditorUI.ColorEdit3("Color", ref Color, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
        return edited;
    }
}
