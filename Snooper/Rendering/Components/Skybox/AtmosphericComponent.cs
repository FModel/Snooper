using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Skybox;

public struct Planet
{
    public Vector3 Position;
    public float Intensity;
    public float Radius;
    public float AtmosphereRadius;
}

[DefaultActorSystem(typeof(SkyboxSystem))]
public class AtmosphericComponent : CubeComponent
{
    public Planet Sun = new()
    {
        Position = new Vector3(0, 1, 0),
        Intensity = 56.0f,
        Radius = 6350e3f,
        AtmosphereRadius = 6400e3f
    };

    public override string Icon => "\uf0c2";

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Atmosphere", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.DragFloat3("Sun Direction", ref Sun.Position, 0.01f, -1.0f, 2.0f);
            EditorUI.DragFloat("Sun Intensity", ref Sun.Intensity, 0.1f, 0.0f);
            EditorUI.DragFloat("Sun Radius", ref Sun.Radius, 1e3f, 0.0f);
            EditorUI.DragFloat("Atmosphere Radius", ref Sun.AtmosphereRadius, 1e3f, Sun.Radius + 1e3f);
        });
    }
}
