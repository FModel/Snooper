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
    private float _aoRadius = 1.0f;
    private float _aoIntensity = 1.0f;
    private float _aoMaxDistance = 80f;
    private int _blurRadius = 2;

    private bool _debug;
    private int _selectedTextureIndex = 0;
    private float _split = 0.5f;

    public void Generate()
    {
        _geometry.Generate();
        _postProcess.Generate();
    }

    public void RenderScene(CameraComponent camera, ICollection<ActorSystem> systems, DirectionalLightComponent? directionalLight)
    {
        var geometrySystems = systems.OfType<IGeometryRenderSystem>().ToArray();
        var computeSystems = systems.OfType<IComputeRenderSystem>().ToArray();
        _geometry.DoRenderPass("Compute Pass", new ComputeRenderContext(camera, computeSystems));

        if (_shadows && directionalLight is { Actor.IsVisible: true })
        {
            var meshSystems = geometrySystems.OfType<IMeshRenderSystem>().ToArray();
            _geometry.DoRenderPass("Shadow Pass", new ShadowRenderContext(camera, directionalLight, meshSystems));
        }

        var context = new GeometryRenderContext(camera, geometrySystems);
        _geometry.DoRenderPass("Deferred Pass", context);
        _geometry.DoRenderPass("Forward Pass", context);
        _geometry.DoRenderPass("Mask Pass", context);
    }

    public void PostProcessScene(CameraComponent camera, ClusteredLightSystem? lightSystem)
    {
        if (_ambientOcclusion)
        {
            _postProcess.DoStagePass("AO Pass", new AmbientOcclusionStageContext(camera, _geometry, _aoRadius, _aoIntensity, _aoMaxDistance));
            _postProcess.DoStagePass("AO Blur Pass", new BlurStageContext(_blurRadius, _geometry));
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
                EditorUI.Property("Radius");
                ImGui.DragFloat("##AO Radius", ref _aoRadius, 0.01f, 0.05f, 10f, "%.2f");

                EditorUI.Property("Intensity");
                ImGui.DragFloat("##AO Intensity", ref _aoIntensity, 0.01f, 0.1f, 4f, "%.2f");

                EditorUI.Property("Max Distance");
                ImGui.DragFloat("##AO Max Distance", ref _aoMaxDistance, 1f, 1f, 20000f, "%.0f");

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
