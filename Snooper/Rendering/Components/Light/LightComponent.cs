using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Objects.Engine;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

[DefaultActorSystem(typeof(ClusteredLightSystem))]
public abstract class LightComponent : BillboardComponent
{
    public float Intensity;
    public readonly ELightUnits IntensityUnits;
    public float IntensityNits;
    public Vector3 Color;
    public readonly bool CastShadows;

    internal BufferAllocation? _allocation;

    public bool IsEnabled { get; internal set; }

    public LightComponent(ULightComponent component, string sprite) : base(component, sprite)
    {
        Intensity = component.Intensity;
        IntensityUnits = component.GetLightUnits();
        IntensityNits = component.GetNitIntensity();

        if (IntensityUnits == ELightUnits.Unitless)
        {
            Intensity *= Settings.GlobalScale;
        }

        Color = component.GetLightColor();
        CastShadows = component.CastShadows;
    }

    public LightComponent(float intensity, Vector3 color, string sprite, Transform? transform = null, string? name = null) : base(sprite, transform, name)
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
        lightData.Intensity = GetFinalIntensity();
    }

    public float GetFinalIntensity() => IntensityNits > 0 ? IntensityNits : Intensity;

    public override string Icon => "\uf0eb";

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Light", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            const float speed = 0.5f;

            EditorUI.Property("Intensity");
            ImGui.BeginDisabled(IntensityNits > 0);
            var edited = ImGui.DragFloat("##Intensity", ref Intensity, speed, 0.0f, float.MaxValue, $"%.1f {IntensityUnits}");
            ImGui.EndDisabled();

            EditorUI.Property("Intensity (nits)");
            edited |= ImGui.DragFloat("##IntensityNits", ref IntensityNits, speed, 0.0f, float.MaxValue, "%.1f nits");

            edited |= DrawLightControls();

            EditorUI.Property("Color");
            edited |= ImGui.ColorEdit3("##Color", ref Color, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

            if (edited)
            {
                MarkDirty(DirtyFlags.Transform); // transform is fine
            }
        });
    }

    protected virtual bool DrawLightControls()
    {
        return false;
    }
}
