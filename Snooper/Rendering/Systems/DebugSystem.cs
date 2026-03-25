using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.UI;
using System.Numerics;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Systems;

public class DebugSystem() : PrimitiveSystem<DebugComponent, PerInstanceData, PerMaterialDebugData>(PrimitiveType.Lines), IControllable
{
    public override uint Order => 50;
    protected override bool AllowDerivation => true;
    protected override bool IsCulled => false;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("default.vert", "debug.frag")
        {
            Geometry = "debug.geom",
            Defines = ["USE_GEOMETRY_SHADER"]
        }
    };

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        shader.SetUniform("uViewportSize", new Vector2(camera.Width, camera.Height));
    }

    private bool _showVisibleMeshBounds;
    private bool _showHiddenMeshBounds;
    private bool _showLandscapeBounds;
    private bool _showPointLightBounds;
    private bool _showSpotLightBounds;
    private bool _showRectLightBounds;
    private bool _showDirectionalLightArrows;
    private bool _showSplineViz;

    public void DrawControls()
    {
        if (ActorManager?.GetSystem<StaticMeshRenderSystem>() is { } meshSystem)
        {
            if (ImGui.Checkbox("Show Visible Static Mesh Bounds", ref _showVisibleMeshBounds))
            {
                Toggle(meshSystem.GetComponents<StaticMeshComponent>().Where(x => x.IsVisible), _showVisibleMeshBounds);
            }

            if (ImGui.Checkbox("Show Hidden Static Mesh Bounds", ref _showHiddenMeshBounds))
            {
                Toggle(meshSystem.GetComponents<StaticMeshComponent>().Where(x => !x.IsVisible), _showHiddenMeshBounds);
            }
        }

        if (ActorManager?.GetSystem<LandscapeSystem>() is { } landscapeSystem)
        {
            if (ImGui.Checkbox("Show Landscape Bounds", ref _showLandscapeBounds))
            {
                Toggle(landscapeSystem.GetComponents<LandscapeMeshComponent>(), _showLandscapeBounds);
            }
        }

        if (ActorManager?.GetSystem<ClusteredLightSystem>() is { } lightSystem)
        {
            if (ImGui.Checkbox("Show Point Light Bounds", ref _showPointLightBounds))
            {
                Toggle(lightSystem.GetComponents<PointLightComponent>(), _showPointLightBounds);
            }
            if (ImGui.Checkbox("Show Spot Light Bounds", ref _showSpotLightBounds))
            {
                Toggle(lightSystem.GetComponents<SpotLightComponent>(), _showSpotLightBounds);
            }
            if (ImGui.Checkbox("Show Rect Light Bounds", ref _showRectLightBounds))
            {
                Toggle(lightSystem.GetComponents<RectLightComponent>(), _showRectLightBounds);
            }
            if (ImGui.Checkbox("Show Directional Light Arrows", ref _showDirectionalLightArrows))
            {
                Toggle(lightSystem.GetComponents<DirectionalLightComponent>(), _showDirectionalLightArrows);
            }
        }

        if (ActorManager?.GetSystem<SplineMeshRenderSystem>() is { } splineSystem)
        {
            if (ImGui.Checkbox("Show Spline Mesh Paths", ref _showSplineViz))
            {
                Toggle(splineSystem.GetComponents<SplineMeshComponent>(), _showSplineViz);
            }
        }
    }

    private void Toggle<TComponent>(IEnumerable<TComponent> components, bool enable) where TComponent : ActorComponent
    {
        foreach (var component in components)
        {
            component.SetDebugVisualizationVisibility(enable);
        }
    }
}
