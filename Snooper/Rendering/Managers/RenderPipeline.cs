using ImGuiNET;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Managers;

public class RenderPipeline : IResizable, IMemoryDetailsProvider, IControllable, IDisposable
{
    public GeometryRenderer Geometry { get; } = new(Settings.DefaultWidthHeight, Settings.DefaultWidthHeight);
    public PostProcessor PostProcess { get; } = new(Settings.DefaultWidthHeight, Settings.DefaultWidthHeight);

    private bool _antiAliasing = true;
    private bool _shadows = true;
    private bool _lighting = false;

    // ao
    private bool _ambientOcclusion = true;
    private int _directionCount = 6;
    private int _stepsPerDirection = 6;

    public void Generate()
    {
        Geometry.Generate();
        PostProcess.Generate();
    }

    public void RenderScene(CameraComponent camera, IShadowSystem[] shadowSystems, ActorSystem[] deferredSystems, ActorSystem[] forwardSystems, DirectionalLightComponent? directionalLight)
    {
        if (_shadows)
        {
            Geometry.DoRenderPass("Shadow Pass", new ShadowRenderContext(camera, directionalLight, shadowSystems));
        }

        Geometry.DoRenderPass("Deferred Pass", new SystemRenderContext(camera, deferredSystems));
        Geometry.DoRenderPass("Forward Pass", new SystemRenderContext(camera, forwardSystems));
    }

    public void PostProcessScene(CameraComponent camera, ClusteredLightSystem? lightSystem)
    {
        if (_ambientOcclusion)
        {
            PostProcess.DoStagePass("SSAO Pass", new AmbientOcclusionStageContext(camera, Geometry, _directionCount, _stepsPerDirection));
        }

        PostProcess.DoStagePass("Lighting Pass", new LitStageContext(camera, Geometry, lightSystem, _ambientOcclusion, _shadows ? Geometry.GetShadowContext() : null));
        PostProcess.DoStagePass("Combine Pass", new GeometryStageContext(Geometry));
    }

    public Texture[] GetFinalTextures() => PostProcess.GetTextures();
    public Texture[] GetGeometryTextures() => Geometry.GetTextures();
    public Texture[] GetAllTextures() => [..PostProcess.GetTextures(), ..Geometry.GetTextures()];

    public void Resize(int newWidth, int newHeight)
    {
        Geometry.Resize(newWidth, newHeight);
        PostProcess.Resize(newWidth, newHeight);
    }

    public void DrawControls()
    {
        ImGui.SeparatorText("Geometry");
        // TODO

        ImGui.SeparatorText("Post-Processing");

        EditorUI.TogglableTreeNode("Anti-Aliasing", ref _antiAliasing, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.Bullet, () => { });
        EditorUI.TogglableTreeNode("Ambient Occlusion", ref _ambientOcclusion, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            EditorUI.PropertyValueTable("Ambient Occlusion", () =>
            {
                EditorUI.Property("Direction Count");
                ImGui.DragInt("##Direction Count", ref _directionCount, 0.05f, 1, 6);

                EditorUI.Property("Steps Per Direction");
                ImGui.DragInt("##Steps Per Direction", ref _stepsPerDirection, 0.05f, 1, 6);
            });
        });
        EditorUI.TogglableTreeNode("Shadows", ref _shadows, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            // _shadow.DrawControls();
        });
        EditorUI.TogglableTreeNode("Lighting", ref _lighting, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            // TODO: refactor CameraFramePair, we need access to systems here
        });
    }

    public long Allocated => Geometry.Allocated + PostProcess.Allocated;
    public long Used => Geometry.Used + PostProcess.Used;

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Geometry Renderer", Geometry);
        yield return new MemoryDetail("Post Processor", PostProcess);
    }

    public void Dispose()
    {
        Geometry.Dispose();
        PostProcess.Dispose();
    }
}
