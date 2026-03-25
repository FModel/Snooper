using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
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

    private bool _debug;
    private int _selectedTextureIndex = 0;
    private float _split = 0.5f;

    public void Generate()
    {
        _geometry.Generate();
        _postProcess.Generate();
    }

    public void RenderScene(CameraComponent camera, IMeshRenderSystem[] meshSystems, IRenderSystem[] renderSystems, DirectionalLightComponent? directionalLight)
    {
        if (_shadows && directionalLight is { Actor.IsVisible: true })
        {
            _geometry.DoRenderPass("Shadow Pass", new ShadowRenderContext(camera, directionalLight, meshSystems));
        }

        var context = new SystemRenderContext(camera, renderSystems);
        _geometry.DoRenderPass("Deferred Pass", context);
        _geometry.DoRenderPass("Forward Pass", context);
        _geometry.DoRenderPass("Mask Pass", context);
    }

    public void PostProcessScene(CameraComponent camera, ClusteredLightSystem? lightSystem)
    {
        if (_ambientOcclusion)
        {
            _postProcess.DoStagePass("AO Pass", new AmbientOcclusionStageContext(camera, _geometry, _directionCount, _stepsPerDirection));
            _postProcess.DoStagePass("AO Blur Pass", new BlurStageContext(_blurRadius));
        }

        var geometryContext = new GeometryStageContext(_geometry);
        var litContext = new LitStageContext(camera, _geometry, lightSystem, _ambientOcclusion, _shadows ? _geometry.GetShadowContext() : null);
        _postProcess.DoStagePass("Lighting Pass", litContext);
        _postProcess.DoStagePass("Combine Pass", geometryContext);
        _postProcess.DoStagePass("Picking Pass", geometryContext);
        _postProcess.DoStagePass("Picking Viz Pass");

        if (_antiAliasing)
        {
            _postProcess.DoStagePass("AA Pass");
        }

        Texture? texture = null;
        if (_debug)
        {
            texture = GetTextures()[_selectedTextureIndex];
            if (texture.Name == "PostProcess - Shadow Viz")
            {
                _postProcess.DoStagePass("Shadow Viz Pass", litContext);
            }
        }

        _postProcess.DoStagePass("Final Pass", new FinalStageContext(_antiAliasing, texture, _split));
    }

    public void RenderToScreen(int width, int height)
    {
        GL.BlitNamedFramebuffer(_postProcess, 0, 0, 0, _postProcess.Width, _postProcess.Height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    }

    public uint GetComponentId(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize) => _postProcess.GetComponentId(mousePos, windowPos, windowSize);

    public Texture GetFinalTexture() => _postProcess.GetFinalTexture();
    public Texture[] GetTextures() => [.._postProcess.GetTextures(), .._geometry.GetTextures()];

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
            // TODO
            _geometry.DrawControls();
        });

        EditorUI.TogglableTreeNode("Debug Options", ref _debug, () =>
        {
            EditorUI.PropertyValueTable("Debug Options", () =>
            {
                var textures = GetTextures();
                EditorUI.Property("Texture");
                if (ImGui.BeginCombo("##Texture Selector", textures[_selectedTextureIndex].Name))
                {
                    for (var i = 0; i < textures.Length; i++)
                    {
                        var isSelected = _selectedTextureIndex == i;
                        if (ImGui.Selectable(textures[i].Name, isSelected))
                        {
                            _selectedTextureIndex = i;
                        }
                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                EditorUI.Property("Vertical Split");
                ImGui.SliderFloat("##Vertical Split", ref _split, 0.0f, 1.0f);
            });
        });
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
