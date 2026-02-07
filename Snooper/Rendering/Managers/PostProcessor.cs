using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Managers;

public class PostProcessor(int originalWidth, int originalHeight) : FullQuadFramebuffer(originalWidth, originalHeight)
{
    private readonly ResizableTexture2D _ssao = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red);
    private readonly ResizableTexture2D _ssaoBlur = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red);
    private readonly ResizableTexture2D _lit = new(originalWidth, originalHeight); // deferred pass with lighting, shadows, and SSAO applied
    private readonly ResizableTexture2D _combined = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _fxaa = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _shadow = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb);

    private readonly List<StagePass> _passes = [];

    private int _frameCount;

    public override void Generate()
    {
        _ssao.Generate();
        _ssao.Resize(Width, Height);
        GL.TextureParameter(_ssao, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_ssao, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _ssaoBlur.Generate();
        _ssaoBlur.Resize(Width, Height);
        GL.TextureParameter(_ssaoBlur, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_ssaoBlur, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _lit.Generate();
        _lit.Resize(Width, Height);
        GL.TextureParameter(_lit, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_lit, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _combined.Generate();
        _combined.Resize(Width, Height);
        GL.TextureParameter(_combined, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_combined, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _fxaa.Generate();
        _fxaa.Resize(Width, Height);
        GL.TextureParameter(_fxaa, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_fxaa, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _shadow.Generate();
        _shadow.Resize(Width, Height);
        GL.TextureParameter(_shadow, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_shadow, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment1, _ssao, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment2, _lit, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment3, _combined, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment4, _ssaoBlur, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment5, _fxaa, 0);
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.ColorAttachment6, _shadow, 0);

        CheckStatus();

        var ssao = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/ssao.frag");
        var lit = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/light_clustered.frag")
        {
            // Defines = ["DEBUG_CLUSTER_GRID_OVERLAY"]
        };
        var combine = new EmbeddedShader("Framebuffers/combine");
        var blur = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/blur.frag");
        var fxaa = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/fxaa.frag");
        var shadow = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/shadow.frag");
        var final = new EmbeddedShader("Framebuffers/combine.vert", "Framebuffers/final.frag");

        ssao.Generate();
        lit.Generate();
        combine.Generate();
        blur.Generate();
        fxaa.Generate();
        shadow.Generate();
        final.Generate();

        ssao.Link();
        lit.Link();
        combine.Link();
        blur.Link();
        fxaa.Link();
        shadow.Link();
        final.Link();

        _passes.Add(new StagePass<AmbientOcclusionStageContext>("AO Pass", ssao, new Vector4(1.0f, 0.0f, 0.0f, 0.0f), ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment1)
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

        _passes.Add(new StagePass<BlurStageContext>("AO Blur Pass", blur, Vector4.Zero, ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment4)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("texelSize", Vector2.One / new Vector2(_ssao.Width, _ssao.Height));
                shader.SetUniform("blurRadius", ctx.Radius);

                _ssao.Bind(0);
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
                    _ssaoBlur.Bind(4);
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

        _passes.Add(new StagePass<NoStageContext>("AA Pass", fxaa, Vector4.Zero, ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment5)
        {
            SetupBindings = (_, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("inverseScreenSize", Vector2.One / new Vector2(Width, Height));

                _combined.Bind(0);
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Shadow Viz Pass", shadow, Vector4.Zero, ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment6)
        {
            SetupBindings = (ctx, shader) =>
            {
                var cascadeCount = ctx.ShadowContext?.Depth ?? 4;
                var gridCols = (int)Math.Ceiling(Math.Sqrt(cascadeCount));
                var gridRows = (int)Math.Ceiling((float)cascadeCount / gridCols);
                var cellSize = new Vector2(1.0f / gridCols, 1.0f / gridRows);

                shader.SetUniform("shadowTexture", 0);
                shader.SetUniform("cameraTexture", 1);
                shader.SetUniform("cascadeCount", cascadeCount);
                shader.SetUniform("gridCols", gridCols);
                shader.SetUniform("gridRows", gridRows);
                shader.SetUniform("cellSize", cellSize);

                ctx.Geometry.Bind(EFramebuffer.Shadow, 0, 0);
                _fxaa.Bind(1);
            }
        });

        _passes.Add(new StagePass<FinalStageContext>("Final Pass", final, Vector4.Zero, ClearBufferMask.ColorBufferBit, DrawBufferMode.ColorAttachment0)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("texture1", 0);
                shader.SetUniform("texture2", 1);
                shader.SetUniform("enabled", ctx.Texture != null);
                shader.SetUniform("split", ctx.Split ?? 1.0f);

                (ctx.AntiAliasing ? _fxaa : _combined).Bind(0);
                ctx.Texture?.Bind(1);
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

    public override Texture[] GetTextures() => [ _ssao, _ssaoBlur, _lit, _combined, _fxaa, _shadow, ..base.GetTextures() ];

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        _ssao.Resize(newWidth, newHeight);
        _ssaoBlur.Resize(newWidth, newHeight);
        _lit.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _shadow.Resize(newWidth, newHeight);
    }
}
