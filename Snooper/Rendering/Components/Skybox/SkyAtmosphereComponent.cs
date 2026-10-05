using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Atmosphere;
using CUE4Parse.UE4.Objects.Core.Math;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Skybox;

[DefaultActorSystem(typeof(SkyboxSystem))]
public class SkyAtmosphereComponent : BillboardComponent
{
    public float PlanetRadius;
    public float AtmosphereHeight;
    public readonly ESkyAtmosphereTransformMode TransformMode;
    public Vector3 RayleighScattering;
    public float RayleighScaleHeight;
    public float MieScattering;
    public float MieAbsorption;
    public float MieScaleHeight;
    public float MieAnisotropy;
    public Vector3 OzoneAbsorption;
    public Vector3 GroundAlbedo;
    public Vector3 LuminanceFactor = Vector3.One;

    public float PlanetTop => TransformMode switch
    {
        ESkyAtmosphereTransformMode.PlanetTopAtComponentTransform => LocalTransform.Position.Y,
        ESkyAtmosphereTransformMode.PlanetCenterAtComponentTransform => LocalTransform.Position.Y + PlanetRadius,
        _ => 0.0f
    };

    public SkyAtmosphereComponent(USkyAtmosphereComponent component) : base(component, "S_SkyAtmosphere")
    {
        const float kilometers = 1e3f;

        PlanetRadius = component.BottomRadius * kilometers;
        AtmosphereHeight = component.AtmosphereHeight * kilometers;
        TransformMode = component.TransformMode;

        RayleighScattering = (Vector3) component.RayleighScattering * (component.RayleighScatteringScale / kilometers);
        RayleighScaleHeight = component.RayleighExponentialDistribution * kilometers;

        MieScattering = Average(component.MieScattering) * (component.MieScatteringScale / kilometers);
        MieAbsorption = Average(component.MieAbsorption) * (component.MieAbsorptionScale / kilometers);
        MieScaleHeight = component.MieExponentialDistribution * kilometers;
        MieAnisotropy = component.MieAnisotropy;

        OzoneAbsorption = (Vector3) component.OtherAbsorption * (component.OtherAbsorptionScale / kilometers);

        GroundAlbedo = component.GroundAlbedo;
        LuminanceFactor = component.SkyLuminanceFactor;
        LuminanceFactor /= MathF.Max(MathF.Max(LuminanceFactor.X, LuminanceFactor.Y), MathF.Max(LuminanceFactor.Z, 1.0f)); // because we don't auto exposure

        float Average(FLinearColor color) => (color.R + color.G + color.B) / 3.0f;
    }

    /// <summary>
    /// earth
    /// </summary>
    public SkyAtmosphereComponent(Transform? transform = null, string? name = null) : base("S_SkyAtmosphere", transform, name)
    {
        PlanetRadius = 6360e3f;
        AtmosphereHeight = 60e3f;
        TransformMode = ESkyAtmosphereTransformMode.PlanetTopAtComponentTransform;

        RayleighScattering = new Vector3(5.802e-6f, 13.558e-6f, 33.1e-6f);
        RayleighScaleHeight = 8e3f;
        MieScattering = 3.996e-6f;
        MieAbsorption = 0.444e-6f;
        MieScaleHeight = 1.2e3f;
        MieAnisotropy = 0.9f; // earth's is 0.8 but this gives glare without bloom, and we don't have bloom yet
        OzoneAbsorption = new Vector3(0.650e-6f, 1.881e-6f, 0.085e-6f);

        GroundAlbedo = new Vector3(170.0f / 255.0f);
    }

    public override string Icon => Settings.CloudIcon;

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Atmosphere", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.ColorEdit3("Luminance Factor", ref LuminanceFactor);
            EditorUI.DragFloat("Planet Radius", ref PlanetRadius, 1e3f, 0.0f);
            EditorUI.DragFloat("Atmosphere Height", ref AtmosphereHeight, 1e2f, 1e3f);
            EditorUI.ColorEdit3("Ground Albedo", ref GroundAlbedo);
        });

        EditorUI.CollapsingTable("Air", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.DragFloat3("Rayleigh Scattering", ref RayleighScattering, 1e-7f, 0.0f, 1e-3f, "%.2e");
            EditorUI.DragFloat("Rayleigh Scale Height", ref RayleighScaleHeight, 1e1f, 1.0f);
            EditorUI.DragFloat("Mie Scattering", ref MieScattering, 1e-7f, 0.0f, 1e-3f, "%.2e");
            EditorUI.DragFloat("Mie Absorption", ref MieAbsorption, 1e-7f, 0.0f, 1e-3f, "%.2e");
            EditorUI.DragFloat("Mie Scale Height", ref MieScaleHeight, 1e1f, 1.0f);
            EditorUI.DragFloat("Mie Anisotropy", ref MieAnisotropy, 0.001f, 0.0f, 0.999f, "%.3f");
            EditorUI.DragFloat3("Ozone Absorption", ref OzoneAbsorption, 1e-7f, 0.0f, 1e-3f, "%.2e");
        });
    }
}
