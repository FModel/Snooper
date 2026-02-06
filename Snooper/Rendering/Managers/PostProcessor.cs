using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Managers;

public class PostProcessor(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly ResizableTexture2D _ssao = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, PixelType.Float);
    private readonly ResizableTexture2D _lit = new(originalWidth, originalHeight); // deferred pass with lighting, shadows, and SSAO applied
    private readonly ResizableTexture2D _combined = new(originalWidth, originalHeight);
    // TODO: blur ssao + fxaa

    private readonly List<StagePass> _passes = [];

    private int _frameCount;

    public override void Generate()
    {
        _ssao.Generate();
        _ssao.Resize(Width, Height);
        GL.TextureParameter(_ssao, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_ssao, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _lit.Generate();
        _lit.Resize(Width, Height);
        GL.TextureParameter(_lit, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_lit, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _combined.Generate();
        _combined.Resize(Width, Height);
        GL.TextureParameter(_combined, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_combined, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _ssao, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment2, _lit, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment3, _combined, 0);

        CheckStatus();

        var ssao = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/ssao.frag");
        var lit = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/light_clustered.frag")
        {
            // Defines = ["DEBUG_CLUSTER_GRID_OVERLAY"]
        };
        var combine = new EmbeddedShader("Framebuffers/combine");

        ssao.Generate();
        lit.Generate();
        combine.Generate();

        ssao.Link();
        lit.Link();
        combine.Link();

        _passes.Add(new StagePass<AmbientOcclusionStageContext>("SSAO Pass", ssao, Vector4.One, ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment1)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("gPosition", 0);
                shader.SetUniform("gNormal", 1);
                shader.SetUniform("uProjectionMatrix", ctx.Camera.ProjectionMatrix);
                shader.SetUniform("radius", 1.5f);
                shader.SetUniform("uDirectionCount", ctx.DirectionCount);
                shader.SetUniform("uStepsPerDirection", ctx.StepsPerDirection);
                shader.SetUniform("uFrameCount", ++_frameCount);

                ctx.Geometry.Bind(EFramebuffer.Deferred, 0, 0);
                ctx.Geometry.Bind(EFramebuffer.Deferred, 1, 1);
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Lighting Pass", lit, new Vector4(0, 0, 0, 1), ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment2)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("gPosition", 0);
                shader.SetUniform("gNormal", 1);
                shader.SetUniform("gColor", 2);
                shader.SetUniform("gSpecular", 3);

                ctx.Geometry.Bind(EFramebuffer.Deferred, 0, 0);
                ctx.Geometry.Bind(EFramebuffer.Deferred, 1, 1);
                ctx.Geometry.Bind(EFramebuffer.Deferred, 2, 2);
                ctx.Geometry.Bind(EFramebuffer.Deferred, 3, 3);

                Matrix4x4.Invert(ctx.Camera.ViewMatrix, out var inverseViewMatrix);
                shader.SetUniform("uInverseViewMatrix", inverseViewMatrix);
                shader.SetUniform("uZNear", ctx.Camera.NearClipPlane);
                shader.SetUniform("uZFar", ctx.Camera.FarClipPlane);

                shader.SetUniform("useSsao", ctx.AmbienOcclusion);
                if (ctx.AmbienOcclusion)
                {
                    _ssao.Bind(4);
                    shader.SetUniform("ssao", 4);
                }

                if (ctx.LightSystem is { IsEnabled: true } system)
                {
                    system.BindForRendering();
                    shader.SetUniform("useLighting", true);
                    shader.SetUniform("uGridDimX", system.GridDimensionX);
                    shader.SetUniform("uGridDimY", system.GridDimensionY);
                    shader.SetUniform("uGridDimZ", system.GridDimensionZ);
                }
                else shader.SetUniform("useLighting", false);

                if (ctx.LightSystem?.GetDirectionalLight() is { Actor.IsVisible: true } light)
                {
                    Matrix4x4.Decompose(light.WorldMatrix, out _, out var rotation, out _);

                    shader.SetUniform("useSunLight", true);
                    shader.SetUniform("uSunDirection", Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation)));
                    shader.SetUniform("uSunColor", light.Color);
                    shader.SetUniform("uSunIntensity", light.Intensity);

                    if (ctx.ShadowContext is { } shadows)
                    {
                        shader.SetUniform("useShadows", true);
                        shader.SetUniform("uShadowMapSize", new Vector3(shadows.Width, shadows.Height, shadows.Depth));
                        shader.SetUniform("uShadowBias", shadows.Bias);
                        shader.SetUniform("uCascadePlaneDistances", shadows.PlaneDistances);
                        shader.SetUniform("uLightViewProjectionMatrices", shadows.Matrices);

                        ctx.Geometry.Bind(EFramebuffer.Shadow, 0, 5);
                        shader.SetUniform("shadowMap", 5);
                    }
                    else shader.SetUniform("useShadows", false);
                }
                else shader.SetUniform("useSunLight", false);
            }
        });

        _passes.Add(new StagePass<GeometryStageContext>("Combine Pass", combine, new Vector4(0.2f, 0.2f, 0.2f, 1), ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment3)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("deferredTexture", 0);
                shader.SetUniform("forwardTexture", 1);
                shader.SetUniform("outlineTexture", 2);

                _lit.Bind(0);
                ctx.Geometry.Bind(EFramebuffer.Forward, 0, 1);
                ctx.Geometry.Bind(EFramebuffer.Outline, 0, 2);
            }
        });
    }

    public void DoStagePass(string name, IStageContext? context = null)
    {
        var stage = _passes.Find(s => s.Name == name);
        if (stage == null) return;

        Bind();
        stage.Run(context ?? new NoStageContext(), Render);
        Unbind();
    }

    public override Texture[] GetTextures() => [ _ssao, _lit, _combined ];

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        _ssao.Resize(newWidth, newHeight);
        _lit.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
    }
}
