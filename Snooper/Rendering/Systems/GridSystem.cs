using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem : PrimitiveSystem<GridComponent>
{
    public override uint Order => 59;
    public override int Capacity => 1;
    protected override bool AllowDerivation => true;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("Grid/grid"),
        [CommandBufferType.Opaque] = new EmbeddedShader("Grid/grid.vert", "Grid/grid_opaque.frag")
    };

    private GridComponent? _component;

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        shader.SetUniform("uFar", camera.FarClipPlane);
        shader.SetUniform("uHeight", _component?.LocalTransform.Position.Y ?? 0);

        if (_component is not null)
        {
            var settings = _component.GridSettings;
            shader.SetUniform("uColor", settings.Tint);

            shader.SetUniform("uCellSize", MathF.Max(settings.CellSize, 1e-4f));
            shader.SetUniform("uLodStep", (float) Math.Max(settings.CellsPerDivision, 2));
            shader.SetUniform("uAdaptive", settings.Adaptive);
            shader.SetUniform("uMinCellPixels", MathF.Max(settings.MinCellPixels, 1.0f));

            shader.SetUniform("uMinorThickness", settings.MinorThickness);
            shader.SetUniform("uMajorThickness", settings.MajorThickness);
            shader.SetUniform("uAxisThickness", settings.AxisThickness);
            shader.SetUniform("uMinorColor", settings.MinorColor);
            shader.SetUniform("uMajorColor", settings.MajorColor);
            shader.SetUniform("uAxisColorX", settings.AxisColorX);
            shader.SetUniform("uAxisColorZ", settings.AxisColorZ);
            shader.SetUniform("uMinorOpacity", settings.MinorOpacity);
            shader.SetUniform("uMajorOpacity", settings.MajorOpacity);
            shader.SetUniform("uOpacity", settings.Opacity);
            shader.SetUniform("uShowAxes", settings.ShowAxes);

            // keep the fade window ordered, an inverted one would make the grid vanish entirely
            shader.SetUniform("uFadeStart", MathF.Min(settings.FadeStart, settings.FadeEnd));
            shader.SetUniform("uFadeEnd", MathF.Max(settings.FadeStart, settings.FadeEnd));

            if (settings is OpaqueGridComponent.OpaqueSettings opaque)
            {
                shader.SetUniform("uCheckerColorA", opaque.CheckerColorA);
                shader.SetUniform("uCheckerColorB", opaque.CheckerColorB);
                shader.SetUniform("uCheckerScale", opaque.CheckerScale);
                shader.SetUniform("uMetallic", opaque.Metallic);
                // a fully zeroed specular target means "not a pbr material" to the lighting pass
                shader.SetUniform("uRoughness", MathF.Max(opaque.Roughness, 0.01f));
            }
        }
    }

    protected override void OnActorComponentAdded(GridComponent component)
    {
        base.OnActorComponentAdded(component);

        _component = component;
    }
}
