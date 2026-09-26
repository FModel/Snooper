using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Objects.Core.Math;
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
    public ELightUnits IntensityUnits;
    public Vector3 Color;
    public readonly bool CastShadows;
    public readonly float MaxDrawDistance;
    public readonly float MaxDistanceFadeRange;

    internal BufferAllocation? _allocation;

    public LightComponent(ULightComponent component, string sprite) : base(component, sprite)
    {
        Intensity = component.Intensity;
        IntensityUnits = component.GetLightUnits();

        Color = component.GetLightColor();
        if (component.bUseTemperature)
        {
            Color *= FLinearColor.MakeFromColorTemperature(component.Temperature);
        }

        CastShadows = component.CastShadows;
        MaxDrawDistance = component.MaxDrawDistance * Settings.GlobalScale;
        MaxDistanceFadeRange = component.MaxDistanceFadeRange * Settings.GlobalScale;
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
        if (IsVisible && IsActorVisibleRecursive) SetLightData(ref data);
        return data;
    }

    protected virtual void SetLightData(ref LightData lightData)
    {
        lightData.Position = WorldMatrix.Translation;
        lightData.Color = Color;
        lightData.Intensity = Intensity;
        lightData.MaxDrawDistance = MaxDrawDistance;
        lightData.MaxDistanceFadeRange = MaxDistanceFadeRange;
    }

    public override string Icon => "\uf0eb";

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Light", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Text("Cast Shadows", CastShadows ? "Yes" : "No");
            if (MaxDrawDistance > 0.0f)
            {
                EditorUI.Text("Draw Distance", $"Min: 0, Max: {MaxDrawDistance}, Fade: {MaxDistanceFadeRange}");
            }

            const float speed = 0.5f;

            EditorUI.Property("Intensity");
            var edited = ImGui.DragFloat("##Intensity", ref Intensity, speed, 0.0f, float.MaxValue, $"%.1f {IntensityUnits}");

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
