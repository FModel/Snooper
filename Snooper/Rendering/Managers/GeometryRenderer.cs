using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.UI;

namespace Snooper.Rendering.Managers;

public class GeometryRenderer(int originalWidth, int originalHeight) : IResizable, IMemoryDetailsProvider, IControllable, IDisposable
{
    private readonly ShadowFramebuffer _shadow = new(2048, 4);
    private readonly DeferredFramebuffer _deferred = new(originalWidth, originalHeight);
    private readonly ForwardFramebuffer _forward = new(originalWidth, originalHeight);
    private readonly MaskFramebuffer _mask = new(originalWidth, originalHeight);

    private readonly List<RenderPass> _passes = [];

    public void Generate()
    {
        _shadow.Generate();
        _passes.Add(new RenderPass<ShadowRenderContext>("Shadow Pass")
        {
            PrePass = _ =>
            {
                _shadow.Bind();
                GL.Clear(ClearBufferMask.DepthBufferBit);

                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Front);
            },
            Execute = ctx =>
            {
                var shadowCameras = _shadow.UpdateCascades(ctx.Camera, ctx.Light);
                foreach (var system in ctx.Systems)
                {
                    system.RenderShadows(shadowCameras);
                }
            },
            PostPass = _ =>
            {
                GL.CullFace(TriangleFace.Back);
                GL.Disable(EnableCap.CullFace);

                _shadow.Unbind();
            }
        });

        _deferred.Generate();
        _passes.Add(new RenderPass<SystemRenderContext>("Deferred Pass")
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
        _passes.Add(new RenderPass<SystemRenderContext>("Forward Pass")
        {
            PrePass = _ =>
            {
                // copy depth from deferred pass
                GL.BlitNamedFramebuffer(_deferred, _forward, 0, 0, _deferred.Width, _deferred.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

                _forward.Bind();
                GL.ClearColor(0, 0, 0, 0);
                GL.Clear(ClearBufferMask.ColorBufferBit);

                GL.Enable(EnableCap.Blend);
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
                GL.Enable(EnableCap.Blend);

                _forward.Unbind();
            }
        });

        _mask.Generate();
        _passes.Add(new RenderPass<SystemRenderContext>("Mask Pass")
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
    public void Bind(EShadowTexture texture, uint unit) => _shadow.Bind(texture, unit);
    public void Bind(EMaskTexture texture, uint unit) => _mask.Bind(texture, unit);

    // TODO: improve, this is ugly
    public ShadowStageContext GetShadowContext() => new(_shadow.Width, _shadow.Height, _shadow.CascadeCount,
        _shadow.Bias, _shadow.CascadePlaneDistances, _shadow.CascadeMatrices);

    public void Resize(int newWidth, int newHeight)
    {
        _shadow.Resize(newWidth, newHeight); // won't do anything
        _deferred.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _mask.Resize(newWidth, newHeight);
    }

    public Texture[] GetTextures() =>
    [
        .._shadow.GetTextures(),
        .._deferred.GetTextures(),
        .._forward.GetTextures(),
        .._mask.GetTextures(),
    ];

    public void DrawControls()
    {
        _shadow.DrawControls();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _shadow.Allocated;
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
            total += _shadow.Used;
            total += _deferred.Used;
            total += _forward.Used;
            total += _mask.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Shadow", _shadow);
        yield return new MemoryDetail("GBuffer", _deferred);
        yield return new MemoryDetail("Forward", _forward);
        yield return new MemoryDetail("Mask", _mask);
    }

    public void Dispose()
    {
        _shadow.Dispose();
        _deferred.Dispose();
        _forward.Dispose();
        _mask.Dispose();
    }
}
