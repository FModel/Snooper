using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.UI;

namespace Snooper.Rendering.Managers;

public class GeometryRenderer(int originalWidth, int originalHeight) : IResizable, IMemoryDetailsProvider, IControllable, IDisposable
{
    internal readonly ShadowFramebuffer _shadows = new();
    private readonly DeferredFramebuffer _deferred = new(originalWidth, originalHeight);
    private readonly ForwardFramebuffer _forward = new(originalWidth, originalHeight);
    private readonly MaskFramebuffer _mask = new(originalWidth, originalHeight);

    private readonly List<RenderPass> _passes = [];

    public void Generate()
    {
        _passes.Add(new RenderPass<ComputeRenderContext>("Compute Pass")
        {
            Execute = ctx =>
            {
                foreach (var system in ctx.Systems)
                {
                    system.Execute(ctx.Camera);
                }
            }
        });

        _passes.Add(new RenderPass<CullRenderContext>("Cull Pass")
        {
            Execute = ctx =>
            {
                foreach (var system in ctx.Systems)
                {
                    system.Cull(ctx.Views.Span);
                }
            }
        });

        _shadows.Generate();
        _passes.Add(new RenderPass<ShadowRenderContext>("Shadow Pass")
        {
            PrePass = _ =>
            {
                _shadows.ApplyPendingChanges();
                _shadows.Bind();

                GL.Enable(EnableCap.DepthClamp);
                GL.PolygonOffset(_shadows.SlopeBias, _shadows.ConstantBias);
                GL.Enable(EnableCap.PolygonOffsetFill);
            },
            Execute = ctx =>
            {
                for (var i = 0; i < _shadowViews.Length; i++)
                {
                    var view = _shadowViews[i];
                    if (!_shadows.NeedsRender(view.Slot)) continue;

                    using var _ = Profiler.Sample($"Cascade {i}");

                    _shadows.BindSlot(view.Slot);
                    GL.Clear(ClearBufferMask.DepthBufferBit);

                    foreach (var system in ctx.Systems)
                    {
                        system.RenderShadowCascade(view);
                    }
                }
            },
            PostPass = _ =>
            {
                GL.Disable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(0.0f, 0.0f);
                GL.Disable(EnableCap.DepthClamp);

                _shadows.Unbind();
            }
        });

        _deferred.Generate();
        _passes.Add(new RenderPass<GeometryRenderContext>("Deferred Pass")
        {
            PrePass = _ =>
            {
                _deferred.Bind();
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                GL.Disable(EnableCap.Blend);
            },
            Execute = ctx =>
            {
                foreach (var system in ctx.Systems)
                {
                    system.Render(ctx.Camera, CommandBufferType.Opaque);
                }
            },
            PostPass = _ =>
            {
                GL.Enable(EnableCap.Blend);

                _deferred.Unbind();
            }
        });

        _forward.Generate();
        _passes.Add(new RenderPass<GeometryRenderContext>("Forward Pass")
        {
            PrePass = _ =>
            {
                // copy depth from deferred pass
                GL.BlitNamedFramebuffer(_deferred, _forward, 0, 0, _deferred.Width, _deferred.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

                _forward.Bind();
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                GL.Enable(EnableCap.Blend);
                GL.BlendFuncSeparate(
                    BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha,
                    BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            },
            Execute = ctx =>
            {
                foreach (var system in ctx.Systems)
                {
                    system.Render(ctx.Camera, CommandBufferType.Transparent);
                }
            },
            PostPass = _ =>
            {
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Disable(EnableCap.Blend);

                _forward.Unbind();
            }
        });

        _mask.Generate();
        _passes.Add(new RenderPass<GeometryRenderContext>("Mask Pass")
        {
            PrePass = _ =>
            {
                _mask.Bind();
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.DepthBufferBit);
            },
            Execute = ctx =>
            {
                foreach (var system in ctx.Systems)
                {
                    system.Render(ctx.Camera, CommandBufferType.Mask);
                }
            },
            PostPass = _ =>
            {
                _mask.Unbind();
            }
        });
    }

    public void DoRenderPass(string name, IRenderContext? context = null)
    {
        _passes.Find(p => p.Name == name)?.Run(context ?? new NoRenderContext());
    }

    public void Bind(EDeferredTexture texture, uint unit) => _deferred.Bind(texture, unit);
    public void Bind(EForwardTexture texture, uint unit) => _forward.Bind(texture, unit);
    public void Bind(EShadowTexture texture, uint unit) => _shadows.Bind(texture, unit);
    public void Bind(EMaskTexture texture, uint unit) => _mask.Bind(texture, unit);

    private readonly CullView[] _cullViews = new CullView[Settings.MaxCullingViews];
    private ShadowMapView[] _shadowViews = [];

    public ReadOnlyMemory<CullView> UpdateViews(CameraComponent camera, DirectionalLightComponent? light)
    {
        _cullViews[0] = new CullView(camera, camera);
        _shadowViews = light is null ? [] : _shadows.UpdateSun(camera, light);

        var count = 1;
        foreach (var view in _shadowViews)
        {
            var index = view.ViewIndex;
            if (index >= _cullViews.Length) continue;

            _cullViews[index] = new CullView(view, camera);
            count = Math.Max(count, index + 1);
        }

        return _cullViews.AsMemory(0, count);
    }

    public void Resize(int newWidth, int newHeight)
    {
        _shadows.Resize(newWidth, newHeight); // won't do anything
        _deferred.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _mask.Resize(newWidth, newHeight);
    }

    public Texture[] GetTextures() =>
    [
        .._shadows.GetTextures(),
        .._deferred.GetTextures(),
        .._forward.GetTextures(),
        .._mask.GetTextures(),
    ];

    public void DrawControls()
    {
        _shadows.DrawControls();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _shadows.Allocated;
            total += _deferred.Allocated;
            total += _forward.Allocated;
            total += _mask.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _shadows.Used;
            total += _deferred.Used;
            total += _forward.Used;
            total += _mask.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Shadow", _shadows);
        yield return new MemoryDetail("GBuffer", _deferred);
        yield return new MemoryDetail("Forward", _forward);
        yield return new MemoryDetail("Mask", _mask);
    }

    public void Dispose()
    {
        _shadows.Dispose();
        _deferred.Dispose();
        _forward.Dispose();
        _mask.Dispose();
    }
}
