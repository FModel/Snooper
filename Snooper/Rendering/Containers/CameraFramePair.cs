using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Containers;

public class CameraFramePair(SceneCameraComponent camera) : IResizable, IMemoryDetailsProvider, IControllable
{
    private const int DefaultWidthHeight = 1;

    public bool IsOpen = true;

    public SceneCameraComponent Camera { get; } = camera;

    private readonly GeometryBuffer _geometry = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly SsaoFramebuffer _ssao = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ForwardFramebuffer _forward = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly CombinedFramebuffer _combined = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly PickingFramebuffer _picking = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ShadowFramebuffer _shadow = new(2048, 4);

    private bool _lighting = false;
    private bool _updateShadows = true;

    public void Generate(int pairIndex)
    {
        Camera.PairIndex = pairIndex;
        TransformSystem.OnTransformComponentUpdated += _ =>
        {
            _updateShadows = true;
        };

        _geometry.Generate();
        _ssao.Generate();
        _forward.Generate();
        _combined.Generate();
        _fxaa.Generate();
        _picking.Generate();
        _shadow.Generate();
    }

    public void DeferredRendering(Action<CameraComponent, ActorSystemType> render, ClusteredLightSystem? lightSystem, DirectionalLightComponent? directionalLightComponent = null)
    {
        _geometry.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Disable(EnableCap.Blend);

        render(Camera, ActorSystemType.Deferred);

        if (Camera.bAmbientOcclusion)
        {
            _ssao.Bind();
            GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssao.Render(shader =>
            {
                _geometry.BindTextures(true, true);
                shader.SetUniform("uProjectionMatrix", Camera.ProjectionMatrix);
                shader.SetUniform("radius", Camera.SsaoRadius);
            });
        }

        _geometry.Render(shader =>
        {
            Matrix4x4.Invert(Camera.ViewMatrix, out var inverseViewMatrix);
            shader.SetUniform("uInverseViewMatrix", inverseViewMatrix);
            shader.SetUniform("uZNear", Camera.NearClipPlane);
            shader.SetUniform("uZFar", Camera.FarClipPlane);

            if (directionalLightComponent is { Actor.IsVisible: true })
            {
                Matrix4x4.Decompose(directionalLightComponent.WorldMatrix, out _, out var rotation, out _);

                shader.SetUniform("useSunLight", true);
                shader.SetUniform("uSunDirection", Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation)));
                shader.SetUniform("uSunColor", directionalLightComponent.Color);
                shader.SetUniform("uSunIntensity", directionalLightComponent.Intensity);

                shader.SetUniform("useShadows", Camera.bShadows);
                if (Camera.bShadows)
                {
                    shader.SetUniform("uShadowMapSize", new Vector2(_shadow.Width, _shadow.Height));
                    shader.SetUniform("uShadowBias", _shadow.Bias);
                    shader.SetUniform("uCascadeCount", _shadow.CascadeCount);
                    shader.SetUniform("uCascadePlaneDistances", _shadow.CascadePlaneDistances);
                    shader.SetUniform("uLightViewProjectionMatrices", _shadow.CascadeMatrices);

                    _shadow.Bind(5);
                    shader.SetUniform("shadowMap", 5);
                }
            }
            else shader.SetUniform("useSunLight", false);

            shader.SetUniform("useSsao", Camera.bAmbientOcclusion);
            if (Camera.bAmbientOcclusion)
            {
                _ssao.Bind(4);
                shader.SetUniform("ssao", 4);
            }

            if (lightSystem is { IsEnabled: true })
            {
                lightSystem.BindForRendering();

                shader.SetUniform("useLighting", true);
                shader.SetUniform("uGridDimX", lightSystem.GridDimensionX);
                shader.SetUniform("uGridDimY", lightSystem.GridDimensionY);
                shader.SetUniform("uGridDimZ", lightSystem.GridDimensionZ);
            }
            else shader.SetUniform("useLighting", false);
        });
    }

    public void ForwardRendering(Action<CameraComponent, ActorSystemType> render)
    {
        // copy depth from gBuffer
        GL.BlitNamedFramebuffer(_geometry, _forward, 0, 0, _geometry.Width, _geometry.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

        _forward.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Enable(EnableCap.Blend);

        render(Camera, ActorSystemType.Forward);
    }

    public void PickingRendering()
    {
        _picking.Bind();
        _geometry.BindPicking(0);
        _forward.BindPicking(1);
        _picking.Render();
    }

    public void ShadowRendering(Action<IViewProjectionProvider[]> render, DirectionalLightComponent? directionalLightComponent = null)
    {
        if (!Camera.bShadows || !_updateShadows || directionalLightComponent is not { Actor.IsVisible: true }) return;

        _shadow.Bind();
        GL.Clear(ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Front);

        var shadowCameras = _shadow.UpdateCascades(Camera, directionalLightComponent);
        render(shadowCameras);

        GL.CullFace(TriangleFace.Back);
        GL.Disable(EnableCap.CullFace);

        _updateShadows = false;
    }

    public void CombineRendering()
    {
        _combined.Bind();
        GL.ClearColor(0.2f, 0.2f, 0.2f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _combined.Render(_ =>
        {
            _geometry.Bind(0);
            _forward.Bind(1);
            _picking.Bind(2);
        });
    }

    public void ApplyFxaa()
    {
        if (!Camera.bFXAA) return;

        _fxaa.Bind();
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _fxaa.Render(_ => _combined.Bind(0));
    }

    public void RenderToScreen(int width, int height)
    {
        FullQuadFramebuffer framebuffer = Camera.bFXAA ? _fxaa : _combined;
        GL.BlitNamedFramebuffer(framebuffer, 0, 0, 0, framebuffer.Width, framebuffer.Height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    }

    public uint ReadPickingPixel(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize) => _picking.ReadPixel(mousePos, windowPos, windowSize);
    public void SetPickedIds(IEnumerable<uint> ids) => _picking.SetPickedIds(ids);

    public void Resize(int newWidth, int newHeight)
    {
        Camera.Resize(newWidth, newHeight);

        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
    }

    public Texture[] GetTextures() =>
    [
        .._geometry.GetTextures(),
        .._ssao.GetTextures(),
        .._forward.GetTextures(),
        .._shadow.GetTextures(),
        ..Camera.bFXAA ? _fxaa.GetTextures() : _combined.GetTextures(),
    ];

    public void DrawControls()
    {
        EditorUI.TogglableTreeNode("Anti-Aliasing", ref Camera.bFXAA, ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.Bullet, () => { });

        EditorUI.TogglableTreeNode("Ambient Occlusion", ref Camera.bAmbientOcclusion, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            _ssao.DrawControls();
        });

        EditorUI.TogglableTreeNode("Shadows", ref Camera.bShadows, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            _shadow.DrawControls();
        });

        EditorUI.TogglableTreeNode("Lighting", ref _lighting, ImGuiTreeNodeFlags.SpanAvailWidth, () =>
        {
            // TODO: refactor CameraFramePair, we need access to systems here
        });

        if (ImGui.TreeNodeEx("Camera", ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            EditorUI.PropertyValueTable("Camera", () =>
            {
                EditorUI.DragFloat("Speed", ref Camera.MovementSpeed, 0.1f, 1.0f, 1000.0f, "%.2f units/s");
                EditorUI.DragFloat("FOV", ref Camera.FieldOfView, 0.1f, 30.0f, 120.0f, "%.2f deg");

                var nearClip = Camera.NearClipPlane;
                var farClip = Camera.FarClipPlane;

                var edited = EditorUI.DragFloat("Near Clip Plane", ref nearClip, 0.1f, 0.01f, farClip - 0.1f);
                edited |= EditorUI.DragFloat("Far Clip Plane", ref farClip, 1.0f, nearClip + 0.1f, 100000.0f);

                if (edited)
                {
                    Camera.NearClipPlane = nearClip;
                    Camera.FarClipPlane = farClip;
                }
            });

            ImGui.TreePop();
        }

        if (ImGui.TreeNodeEx("Debug Options", ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            ImGui.TreePop();
        }
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _geometry.Allocated;
            total += _ssao.Allocated;
            total += _forward.Allocated;
            total += _combined.Allocated;
            total += _fxaa.Allocated;
            total += _picking.Allocated;
            total += _shadow.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _geometry.Used;
            total += _ssao.Used;
            total += _forward.Used;
            total += _combined.Used;
            total += _fxaa.Used;
            total += _picking.Used;
            total += _shadow.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("GBuffer", _geometry);
        yield return new MemoryDetail("SSAO", _ssao);
        yield return new MemoryDetail("Forward", _forward);
        yield return new MemoryDetail("Combined", _combined);
        yield return new MemoryDetail("FXAA", _fxaa);
        yield return new MemoryDetail("Picking", _picking);
        yield return new MemoryDetail("Shadow", _shadow);
    }
}
