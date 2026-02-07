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
    private readonly GeometryRenderer _geometry = new(Settings.DefaultWidthHeight, Settings.DefaultWidthHeight);
    private readonly PostProcessor _postProcess = new(Settings.DefaultWidthHeight, Settings.DefaultWidthHeight);

    private bool _antiAliasing = true;
    private bool _shadows = true;

    // ao
    private bool _ambientOcclusion = true;
    private int _directionCount = 6;
    private int _stepsPerDirection = 6;
    private int _blurRadius = 2;

    private bool _debug = false;
    private int _index = 0;
    private float _split = 0.5f;

    public void Generate()
    {
        _geometry.Generate();
        _postProcess.Generate();
    }

    public void RenderScene(CameraComponent camera, IShadowSystem[] shadowSystems, ActorSystem[] deferredSystems, ActorSystem[] forwardSystems, DirectionalLightComponent? directionalLight)
    {
        if (_shadows && directionalLight is { Actor.IsVisible: true })
        {
            _geometry.DoRenderPass("Shadow Pass", new ShadowRenderContext(camera, directionalLight, shadowSystems));
        }

        _geometry.DoRenderPass("Deferred Pass", new SystemRenderContext(camera, deferredSystems));
        _geometry.DoRenderPass("Forward Pass", new SystemRenderContext(camera, forwardSystems));
    }

    public void PostProcessScene(CameraComponent camera, ClusteredLightSystem? lightSystem)
    {
        if (_ambientOcclusion)
        {
            _postProcess.DoStagePass("AO Pass", new AmbientOcclusionStageContext(camera, _geometry, _directionCount, _stepsPerDirection));
            _postProcess.DoStagePass("AO Blur Pass", new BlurStageContext(_blurRadius));
        }

        var context = new LitStageContext(camera, _geometry, lightSystem, _ambientOcclusion, _shadows ? _geometry.GetShadowContext() : null);
        _postProcess.DoStagePass("Lighting Pass", context);
        _postProcess.DoStagePass("Combine Pass", new GeometryStageContext(_geometry));

        if (_antiAliasing)
        {
            _postProcess.DoStagePass("AA Pass");
        }

        if (_debug && _index == 5)
        {
            _postProcess.DoStagePass("Shadow Viz Pass", context);
        }

        _postProcess.DoStagePass("Final Pass", new FinalStageContext(_antiAliasing, _debug ? GetAllTextures()[_index] : null, _split));
    }

    public Texture[] GetFinalTextures() => _postProcess.GetTextures();
    public Texture[] GetGeometryTextures() => _geometry.GetTextures();
    public Texture[] GetAllTextures() => [..GetFinalTextures(), ..GetGeometryTextures()];
    public Texture GetFinalTexture() => GetFinalTextures()[^1];

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _postProcess.Resize(newWidth, newHeight);
    }

    public void DrawControls()
    {
        ImGui.SeparatorText("Post-Processing");

        EditorUI.TogglableTreeNode("Anti-Aliasing", ref _antiAliasing);
        EditorUI.TogglableTreeNode("Ambient Occlusion", ref _ambientOcclusion, () =>
        {
            EditorUI.PropertyValueTable("Ambient Occlusion", () =>
            {
                EditorUI.Property("Direction Count");
                ImGui.DragInt("##Direction Count", ref _directionCount, 0.05f, 1, 6);

                EditorUI.Property("Steps Per Direction");
                ImGui.DragInt("##Steps Per Direction", ref _stepsPerDirection, 0.05f, 1, 6);

                EditorUI.Property("Blur Radius");
                ImGui.DragInt("##Blur Radius", ref _blurRadius, 0.05f, 0, 10);
            });
        });
        EditorUI.TogglableTreeNode("Shadows", ref _shadows, () =>
        {
            _geometry.DrawControls();
        });

        _debug = ImGui.TreeNodeEx("Debug Options", ImGuiTreeNodeFlags.SpanAvailWidth);
        if (_debug)
        {
            EditorUI.PropertyValueTable("Debug Options", () =>
            {
                EditorUI.Property("Texture Index");
                ImGui.DragInt("##Texture Index", ref _index, 0.01f, 0, GetAllTextures().Length - 1);

                EditorUI.Property("Vertical Split");
                ImGui.SliderFloat("##Vertical Split", ref _split, 0.0f, 1.0f);
            });
            ImGui.TreePop();
        }
    }

    public long Allocated => _geometry.Allocated + _postProcess.Allocated;
    public long Used => _geometry.Used + _postProcess.Used;

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Geometry Renderer", _geometry);
        yield return new MemoryDetail("Post Processor", _postProcess);
    }

    public void Dispose()
    {
        _geometry.Dispose();
        _postProcess.Dispose();
    }
}
