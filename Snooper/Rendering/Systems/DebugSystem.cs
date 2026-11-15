using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;
using System.Numerics;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Visualization;

namespace Snooper.Rendering.Systems;

public class DebugSystem() : PrimitiveSystem<DebugComponent, PerInstanceData, PerMaterialDebugData>(PrimitiveType.Lines), IControllable
{
    public override ActorSystemType SystemType => ActorSystemType.Overlay;
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
    private bool _showMeshCollision;
    private bool _showLandscapeBounds;
    private bool _showPointLightBounds;
    private bool _showSpotLightBounds;
    private bool _showRectLightBounds;
    private bool _showDirectionalLightArrows;
    private bool _showSplineViz;

    public void DrawControls()
    {
        if (ActorManager?.GetSystemsInternal<IMeshRenderSystem>() is { } systems)
        {
            if (ImGui.Checkbox("Show Visible Mesh Bounds", ref _showVisibleMeshBounds))
            {
                foreach (var system in systems)
                {
                    Toggle<MeshBoundsVisualization>(system.GetMeshComponents().Where(x => x.IsVisible), _showVisibleMeshBounds);
                }
            }

            if (ImGui.Checkbox("Show Hidden Mesh Bounds", ref _showHiddenMeshBounds))
            {
                foreach (var system in systems)
                {
                    Toggle<MeshBoundsVisualization>(system.GetMeshComponents().Where(x => !x.IsVisible), _showHiddenMeshBounds);
                }
            }

            if (ImGui.Checkbox("Show Mesh Collision", ref _showMeshCollision))
            {
                foreach (var system in systems)
                {
                    foreach (var component in system.GetMeshComponents())
                    {
                        component.SetVisualizationVisibility<CollisionComponentVisualization>(_showMeshCollision && component.IsVisible);
                    }
                }
            }
        }

        if (ActorManager?.GetSystem<LandscapeSystem>() is { } landscapeSystem)
        {
            if (ImGui.Checkbox("Show Landscape Bounds", ref _showLandscapeBounds))
            {
                Toggle<LandscapeMeshComponentVisualization>(landscapeSystem.GetComponents<LandscapeMeshComponent>(), _showLandscapeBounds);
            }
        }

        if (ActorManager?.GetSystem<ClusteredLightSystem>() is { } lightSystem)
        {
            if (ImGui.Checkbox("Show Point Light Bounds", ref _showPointLightBounds))
            {
                Toggle<PointLightComponentVisualization>(lightSystem.GetComponents<PointLightComponent>(), _showPointLightBounds);
            }
            if (ImGui.Checkbox("Show Spot Light Bounds", ref _showSpotLightBounds))
            {
                Toggle<SpotLightComponentVisualization>(lightSystem.GetComponents<SpotLightComponent>(), _showSpotLightBounds);
            }
            if (ImGui.Checkbox("Show Rect Light Bounds", ref _showRectLightBounds))
            {
                Toggle<RectLightComponentVisualization>(lightSystem.GetComponents<RectLightComponent>(), _showRectLightBounds);
            }
        }

        if (ActorManager?.GetSystem<DirectionalLightSystem>() is { } sunSystem)
        {
            if (ImGui.Checkbox("Show Directional Light Arrows", ref _showDirectionalLightArrows))
            {
                Toggle<DirectionalLightComponentVisualization>(sunSystem.GetComponents<DirectionalLightComponent>(), _showDirectionalLightArrows);
            }
        }

        if (ActorManager?.GetSystem<SplineMeshRenderSystem>() is { } splineSystem)
        {
            if (ImGui.Checkbox("Show Spline Mesh Paths", ref _showSplineViz))
            {
                Toggle<SplineMeshComponentVisualization>(splineSystem.GetComponents<SplineMeshComponent>(), _showSplineViz);
            }
        }
    }

    private static void Toggle<T>(IEnumerable<ActorComponent> components, bool enable) where T : SpatialComponent, IVisualization<T>
    {
        foreach (var component in components)
        {
            component.SetVisualizationVisibility<T>(enable);
        }
    }
}
