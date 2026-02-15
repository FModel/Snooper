using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Objects.Engine;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

[DefaultActorSystem(typeof(ClusteredLightSystem))]
public abstract class LightComponent : SpatialComponent
{
    public readonly float Intensity;
    public readonly ELightUnits IntensityUnits;
    public readonly float IntensityNits;
    public readonly Vector3 Color;
    public readonly bool CastShadows;

    internal BufferAllocation? _allocation;

    public LightComponent(ULightComponent component) : base(component)
    {
        Intensity = component.Intensity;
        IntensityUnits = component.GetLightUnits();
        IntensityNits = component.GetNitIntensity();

        Color = component.GetLightColor();
        CastShadows = component.CastShadows != 0;
    }

    public LightComponent(float intensity, Vector3 color, Transform? transform = null, string? name = null) : base(transform, name)
    {
        Intensity = intensity;
        Color = color;
        CastShadows = true;
    }

    public LightData GetLightData()
    {
        var data = new LightData();
        SetLightData(ref data);
        return data;
    }

    protected virtual void SetLightData(ref LightData lightData)
    {
        lightData.Position = WorldMatrix.Translation;
        lightData.Color = Color;
        lightData.Intensity = IntensityNits;
    }

    internal override string Icon => "bulb";

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Light", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Intensity", $"{Intensity:F} {IntensityUnits}");
            EditorUI.Text("Intensity (nits)", $"{IntensityNits:F} nits");
            EditorUI.Property("Color");
            ImGui.ColorButton("##Color", new Vector4(Color, 1.0f), ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoTooltip);

            DrawLightControls();
        });
    }

    protected virtual void DrawLightControls()
    {
    }
}
