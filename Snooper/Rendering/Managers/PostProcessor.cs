using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Managers;

public class PostProcessor(int originalWidth, int originalHeight) : FullQuadFramebuffer<EPostProcessTexture>(originalWidth, originalHeight)
{
    private readonly ResizableTexture2D _ssao = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, name: "PostProcess - SSAO");
    private readonly ResizableTexture2D _ssaoBlur = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, name: "PostProcess - SSAO Blur");
    private readonly ResizableTexture2D _lit = new(originalWidth, originalHeight, name: "PostProcess - Lit"); // deferred pass with lighting, shadows, and SSAO applied
    private readonly ResizableTexture2D _combined = new(originalWidth, originalHeight, name: "PostProcess - Combined");
    private readonly ResizableTexture2D _fxaa = new(originalWidth, originalHeight, name: "PostProcess - FXAA");
    private readonly ResizableTexture2D _shadowViz = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb, name: "PostProcess - Shadow Viz");
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _pickingViz = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb, name: "PostProcess - Picking Viz");

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

        _shadowViz.Generate();
        _shadowViz.Resize(Width, Height);
        GL.TextureParameter(_shadowViz, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_shadowViz, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        _picking.Generate();
        _picking.Resize(Width, Height);

        _pickingViz.Generate();
        _pickingViz.Resize(Width, Height);
        GL.TextureParameter(_pickingViz, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TextureParameter(_pickingViz, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

        base.Generate();
        // ColorAttachment0 = final output (outputted to screen)
        // other attachments are for intermediate steps and may be ping-ponged as needed

        const string vertex = "Framebuffers/fullquad.vert";
        var ssao = new EmbeddedShader(vertex, "Framebuffers/ssao.frag");
        var lit = new EmbeddedShader(vertex, "Framebuffers/light_clustered.frag")
        {
            // Defines = ["DEBUG_CLUSTER_GRID_OVERLAY"]
        };
        var combine = new EmbeddedShader(vertex, "Framebuffers/combine.frag");
        var blur = new EmbeddedShader(vertex, "Framebuffers/blur.frag");
        var fxaa = new EmbeddedShader(vertex, "Framebuffers/fxaa.frag");
        var shadow = new EmbeddedShader(vertex, "Framebuffers/shadow.frag");
        var final = new EmbeddedShader(vertex, "Framebuffers/final.frag");
        var picking = new EmbeddedShader(vertex, "Picking/combine.frag");
        var pickingViz = new EmbeddedShader(vertex, "Picking/visualize.frag");

        ssao.Generate();
        lit.Generate();
        combine.Generate();
        blur.Generate();
        fxaa.Generate();
        shadow.Generate();
        final.Generate();
        picking.Generate();
        pickingViz.Generate();

        ssao.Link();
        lit.Link();
        combine.Link();
        blur.Link();
        fxaa.Link();
        shadow.Link();
        final.Link();
        picking.Link();
        pickingViz.Link();

        _passes.Add(new StagePass<AmbientOcclusionStageContext>("AO Pass", ssao, _ssao)
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

                ctx.Geometry.Bind(EDeferredTexture.Position, 0);
                ctx.Geometry.Bind(EDeferredTexture.Normal, 1);
            }
        });

        _passes.Add(new StagePass<BlurStageContext>("AO Blur Pass", blur, _ssaoBlur)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("texelSize", Vector2.One / new Vector2(_ssao.Width, _ssao.Height));
                shader.SetUniform("blurRadius", ctx.Radius);

                _ssao.Bind(0);  // Read from attachment 1, write to attachment 2
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Lighting Pass", lit, _lit)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("gPosition", 0);
                shader.SetUniform("gNormal", 1);
                shader.SetUniform("gColor", 2);
                shader.SetUniform("gSpecular", 3);

                ctx.Geometry.Bind(EDeferredTexture.Position, 0);
                ctx.Geometry.Bind(EDeferredTexture.Normal, 1);
                ctx.Geometry.Bind(EDeferredTexture.Color, 2);
                ctx.Geometry.Bind(EDeferredTexture.Specular, 3);

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

                        ctx.Geometry.Bind(EShadowTexture.Depth, 5);
                        shader.SetUniform("shadowMap", 5);
                    }
                    else shader.SetUniform("useShadows", false);
                }
                else shader.SetUniform("useSunLight", false);
            }
        });

        _passes.Add(new StagePass<GeometryStageContext>("Combine Pass", combine, _combined)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("deferredTexture", 0);
                shader.SetUniform("forwardTexture", 1);
                shader.SetUniform("maskTexture", 2);
                shader.SetUniform("texelSize", Vector2.One / new Vector2(Width, Height));
                shader.SetUniform("outlineThickness", 2);
                shader.SetUniform("outlineColor", new Vector3(1.0f, 0.6f, 0.2f)); // Orange outline

                _lit.Bind(0);
                ctx.Geometry.Bind(EForwardTexture.Color, 1);
                ctx.Geometry.Bind(EMaskTexture.Depth, 2);
            }
        });

        _passes.Add(new StagePass<NoStageContext>("AA Pass", fxaa, _fxaa)
        {
            SetupBindings = (_, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("inverseScreenSize", Vector2.One / new Vector2(Width, Height));

                _combined.Bind(0);  // Read from attachment 2, write to attachment 1
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Shadow Viz Pass", shadow, _shadowViz)
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

                ctx.Geometry.Bind(EShadowTexture.Depth, 0);
                _fxaa.Bind(1);
            }
        });

        _passes.Add(new StagePass<FinalStageContext>("Final Pass", final)
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

        _passes.Add(new StagePass<GeometryStageContext>("Picking Pass", picking, _picking)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("deferredPicking", 0);
                shader.SetUniform("forwardPicking", 1);

                ctx.Geometry.Bind(EDeferredTexture.Picking, 0);
                ctx.Geometry.Bind(EForwardTexture.Picking, 1);
            }
        });

        _passes.Add(new StagePass<NoStageContext>("Picking Viz Pass", pickingViz, _pickingViz)
        {
            SetupBindings = (_, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                _picking.Bind(0);  // Read from attachment 1, write to attachment 2
            }
        });
    }

    public void DoStagePass(string name, IStageContext? context = null)
    {
        var stage = _passes.Find(s => s.Name == name);
        if (stage == null) return;

        Bind();
        stage.Run(context ?? new NoStageContext(), Handle, Render);
        Unbind();
    }

    public override void Bind(EPostProcessTexture texture, uint unit)
    {
        var t = texture switch
        {
            EPostProcessTexture.Ao => _ssao,
            EPostProcessTexture.AoBlur => _ssaoBlur,
            EPostProcessTexture.Lit => _lit,
            EPostProcessTexture.Combined => _combined,
            EPostProcessTexture.PickingViz => _pickingViz,
            EPostProcessTexture.Aa => _fxaa,
            EPostProcessTexture.ShadowViz => _shadowViz,
            _ => throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid post-process texture type")
        };

        t.Bind(unit);
    }

    public uint GetComponentId(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        var scaleX = windowSize.X / Width;
        var scaleY = windowSize.Y / Height;
        var x = Convert.ToInt32((mousePos.X - windowPos.X) / scaleX);
        var y = Convert.ToInt32((mousePos.Y - windowPos.Y) / scaleY);

        // ui disabled / enabled
        if (windowPos == Vector2.Zero)
            y = Height - 1 - y;
        else
        {
            y += 4; // don't ask me why
            y = -y;
        }

        var pixel = _picking.GetPixel<uint>(x, y);
        Log.Debug("Read pixel at ({X}, {Y}) with scale ({ScaleX}, {ScaleY}), resulting in component ID {ComponentId}", x, y, scaleX, scaleY, pixel);

        return pixel;
    }

    public Texture GetFinalTexture() => base.GetTextures()[^1];
    public override Texture[] GetTextures() =>
    [
        _ssao,
        _ssaoBlur,
        _lit,
        _combined,
        _fxaa,
        _pickingViz,
        _shadowViz,
    ];

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        _ssao.Resize(newWidth, newHeight);
        _ssaoBlur.Resize(newWidth, newHeight);
        _lit.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _shadowViz.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _pickingViz.Resize(newWidth, newHeight);
    }
}
