using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Skybox;

[DefaultActorSystem(typeof(FogSystem))]
public class ExponentialHeightFogComponent : BillboardComponent
{
    public float Density;
    public float HeightFalloff;
    public float MaxOpacity;
    public float StartDistance;
    public Vector3 InscatteringColor;
    public Vector3 DirectionalInscatteringColor;
    public float DirectionalInscatteringExponent;
    public float DirectionalInscatteringStartDistance;

    public float Height => WorldMatrix.Translation.Y;

    public ExponentialHeightFogComponent(UExponentialHeightFogComponent component) : base(component, "S_ExpoHeightFog")
    {
        const float perMeter = 0.001f / Settings.GlobalScale * 0.6931472f;

        Density = component.FogDensity * perMeter;
        HeightFalloff = component.FogHeightFalloff * perMeter;
        MaxOpacity = component.FogMaxOpacity;
        StartDistance = component.StartDistance * Settings.GlobalScale;
        InscatteringColor = component.FogInscatteringLuminance;
        DirectionalInscatteringColor = component.DirectionalInscatteringLuminance;
        DirectionalInscatteringExponent = component.DirectionalInscatteringExponent;
        DirectionalInscatteringStartDistance = component.DirectionalInscatteringStartDistance * Settings.GlobalScale;
    }

    public ExponentialHeightFogComponent(Vector3 color, float density = 0.002f, float heightFalloff = 0.02f, Transform? transform = null, string? name = null) : base("S_ExpoHeightFog", transform, name)
    {
        Density = density;
        HeightFalloff = heightFalloff;
        MaxOpacity = 1.0f;
        InscatteringColor = color;
        DirectionalInscatteringExponent = 4.0f;
    }

    public override string Icon => Settings.SmogIcon;

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Fog", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.DragFloat("Density", ref Density, 0.0001f, 0.0f, 1.0f, "%.4f");
            EditorUI.DragFloat("Height Falloff", ref HeightFalloff, 0.001f, 0.0f, 1.0f, "%.3f");
            EditorUI.DragFloat("Max Opacity", ref MaxOpacity, 0.005f, 0.0f, 1.0f);
            EditorUI.DragFloat("Start Distance", ref StartDistance, 0.5f, 0.0f);
            EditorUI.ColorEdit3("Inscattering", ref InscatteringColor);
            EditorUI.ColorEdit3("Directional Inscattering", ref DirectionalInscatteringColor);
            EditorUI.DragFloat("Directional Exponent", ref DirectionalInscatteringExponent, 0.05f, 1.0f, 64.0f);
            EditorUI.DragFloat("Directional Start", ref DirectionalInscatteringStartDistance, 0.5f, 0.0f);
        });
    }
}
