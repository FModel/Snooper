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
public abstract class LocalLightComponent : LightComponent
{
    public float AttenuationRadius;

    internal BufferAllocation? _allocation;

    public LocalLightComponent(ULocalLightComponent component, string sprite) : base(component, sprite)
    {
        AttenuationRadius = component.AttenuationRadius * Settings.GlobalScale;

        // the shader wants candela from every local light
        float? cosHalfConeAngle = component switch
        {
            UPointLightComponent { bUseInverseSquaredFalloff: false } => null,
            USpotLightComponent spot => spot.GetCosHalfConeAngle(),
            UPointLightComponent => -1.0f,
            _ => 0.0f
        };

        if (cosHalfConeAngle is { } cosine)
        {
            Intensity = IntensityUnits == ELightUnits.EV
                ? LightUtils.EV100ToLuminance(Intensity)
                : Intensity * LightUtils.GetUnitsConversionFactor(IntensityUnits, ELightUnits.Candelas, cosine);
            IntensityUnits = ELightUnits.Candelas;
        }
    }

    protected LocalLightComponent(float attenuationRadius, float intensity, Vector3 color, bool castShadows, string sprite, Transform? transform = null, string? name = null) : base(intensity, color, castShadows, sprite, transform, name)
    {
        AttenuationRadius = attenuationRadius;
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
        lightData.Type = uint.MaxValue; // to override in child classes
        lightData.Range = AttenuationRadius;
    }

    protected override bool DrawLightControls()
    {
        var edited = base.DrawLightControls();

        EditorUI.Property("Attenuation Radius");
        edited |= ImGui.DragFloat("##AttenuationRadius", ref AttenuationRadius, 0.1f, 0f, float.MaxValue, "%.1f");

        if (edited) MarkDirty(DirtyFlags.Transform); // transform is fine
        return edited;
    }
}
