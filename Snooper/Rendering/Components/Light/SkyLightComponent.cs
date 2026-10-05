using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component.Lights;
using CUE4Parse.UE4.Assets.Exports.Texture;
using ImGuiNET;
using Snooper.Core;
using Snooper.Extensions;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Light;

[DefaultActorSystem(typeof(SkyLightSystem))]
public class SkyLightComponent : LightComponentBase
{
    public Vector3 LowerHemisphereColor;
    public readonly ESkyLightSourceType SourceType;

    private readonly string? _path;
    private readonly string? _name;

    public SkyLightComponent(USkyLightComponent component) : base(component, "SkyLight")
    {
        LowerHemisphereColor = component.bLowerHemisphereIsBlack ? component.LowerHemisphereColor * Color : Color;
        SourceType = component.SourceType;

        if (SourceType == ESkyLightSourceType.SLS_SpecifiedCubemap && component.Cubemap?.TryLoad<UTextureCube>(out var cube) == true && Average(cube) is { } average)
        {
            _path = cube.GetCleanPath();
            _name = cube.Name;

            LowerHemisphereColor = average.Lower * Color;
            Color *= average.Upper;
        }

        (Vector3 Upper, Vector3 Lower)? Average(UTextureCube texture)
        {
            Span<Vector3> faces = stackalloc Vector3[6];
            if (!texture.TryGetFaceAverages(faces)) return null;

            const float facing = 0.554f;
            const float around = 0.1115f;
            var sides = (faces[0] + faces[1] + faces[2] + faces[3]) * around;
            return (faces[4] * facing + sides, component.bLowerHemisphereIsBlack ? component.LowerHemisphereColor : faces[5] * facing + sides);
        }
    }

    public SkyLightComponent(Vector3 color, Vector3 lowerHemisphereColor, float intensity = 1.0f, Transform? transform = null, string? name = null) : base(intensity, color, false, "SkyLight", transform, name)
    {
        LowerHemisphereColor = lowerHemisphereColor;
    }

    public override string Icon => Settings.CloudSunIcon;

    private PropertyToggleButton[] CubemapButtons => field ??=
    [
        new PropertyToggleButton(
            () => Settings.CopyIcon,
            () => ImGui.SetClipboardText(_path),
            () => "Copy Path")
    ];

    protected override bool DrawLightControls()
    {
        var edited = base.DrawLightControls();

        edited |= EditorUI.ColorEdit3("Lower Hemisphere", ref LowerHemisphereColor);
        EditorUI.Text("Source Type", SourceType.ToString());
        if (_name != null)
        {
            EditorUI.PropertyWithToggle("Cubemap", CubemapButtons);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(_name);
        }

        return edited;
    }
}
