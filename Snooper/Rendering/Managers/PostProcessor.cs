using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public class PostProcessor(int originalWidth, int originalHeight) : FullQuadFramebuffer<EPostProcessTexture>(originalWidth, originalHeight)
{
    private readonly ResizableTexture2D _ssao = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, name: "PostProcess - SSAO");
    private readonly ResizableTexture2D _ssaoBlur = new(originalWidth, originalHeight, SizedInternalFormat.R8, PixelFormat.Red, name: "PostProcess - SSAO Blur");
    private readonly ResizableTexture2D _lit = new(originalWidth, originalHeight, name: "PostProcess - Lit"); // deferred pass with lighting, shadows, and SSAO applied
    private readonly ResizableTexture2D _combined = new(originalWidth, originalHeight, name: "PostProcess - Combined");
    private readonly ResizableTexture2D _fxaa = new(originalWidth, originalHeight, name: "PostProcess - FXAA");
    private readonly ResizableTexture2D _shadowViz = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb, name: "PostProcess - Shadow Viz");
    private readonly ResizableTexture2D _clusterViz = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb, name: "PostProcess - Light Cluster Viz");
    private readonly PickingTexture _picking = new(originalWidth, originalHeight);
    private readonly ResizableTexture2D _pickingViz = new(originalWidth, originalHeight, SizedInternalFormat.Rgb8, PixelFormat.Rgb, name: "PostProcess - Picking Viz");

    private readonly List<StagePass> _passes = [];

    public override void Generate()
    {
        Generate(_ssao);
        Generate(_ssaoBlur);
        Generate(_lit);
        Generate(_combined);
        Generate(_fxaa);
        Generate(_shadowViz);
        Generate(_clusterViz);
        Generate(_picking, TextureMinFilter.Nearest);
        Generate(_pickingViz);

        base.Generate();
        // ColorAttachment0 = final output (outputted to screen)
        // other attachments are for intermediate steps and may be ping-ponged as needed

        _passes.Add(new StagePass<AmbientOcclusionStageContext>("AO Pass", Generate("Framebuffers/ssao.frag"), _ssao)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("gPosition", 0);
                shader.SetUniform("gNormal", 1);
                shader.SetUniform("uProjectionMatrix", ctx.Camera.ProjectionMatrix);
                shader.SetUniform("radius", ctx.Radius);
                shader.SetUniform("uIntensity", ctx.Intensity);
                shader.SetUniform("uMaxDistance", ctx.MaxDistance);

                ctx.Geometry.Bind(EDeferredTexture.Position, 0);
                ctx.Geometry.Bind(EDeferredTexture.Normal, 1);
            }
        });

        _passes.Add(new StagePass<BlurStageContext>("AO Blur Pass", Generate("Framebuffers/blur.frag"), _ssaoBlur)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("gPosition", 1);
                shader.SetUniform("gNormal", 2);
                shader.SetUniform("texelSize", Vector2.One / new Vector2(_ssao.Width, _ssao.Height));
                shader.SetUniform("blurRadius", ctx.Radius);

                _ssao.Bind(0);  // Read from attachment 1, write to attachment 2
                ctx.Geometry.Bind(EDeferredTexture.Position, 1);
                ctx.Geometry.Bind(EDeferredTexture.Normal, 2);
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Lighting Pass", Generate("Framebuffers/light_clustered.frag", ClusteredLightSystem.LightingDefines), _lit)
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

                shader.SetUniform("uInverseViewMatrix", ctx.Camera.InverseViewMatrix);
                shader.SetUniform("uZNear", ctx.Camera.NearClipPlane);
                shader.SetUniform("uZFar", ctx.Camera.FarClipPlane);

                shader.SetUniform("useSsao", ctx.AmbientOcclusion);
                if (ctx.AmbientOcclusion)
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

                if (ctx.LightSystem?.DirectionalLight is { IsEnabled: true, Actor.IsVisible: true } light)
                {
                    Matrix4x4.Decompose(light.WorldMatrix, out _, out var rotation, out _);

                    shader.SetUniform("useSunLight", true);
                    shader.SetUniform("uSunDirection", Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation)));
                    shader.SetUniform("uSunColor", light.Color);
                    shader.SetUniform("uSunIntensity", light.GetFinalIntensity());

                    if (ctx.Shadows is { } shadows)
                    {
                        shader.SetUniform("useShadows", true);
                        shadows.BindForRendering(shader, 5);
                    }
                    else shader.SetUniform("useShadows", false);
                }
                else shader.SetUniform("useSunLight", false);
            }
        });

        _passes.Add(new StagePass<GeometryStageContext>("Combine Pass", Generate("Framebuffers/combine.frag"), _combined)
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

        _passes.Add(new StagePass<NoStageContext>("AA Pass", Generate("Framebuffers/fxaa.frag"), _fxaa)
        {
            SetupBindings = (_, shader) =>
            {
                shader.SetUniform("inputTexture", 0);
                shader.SetUniform("inverseScreenSize", Vector2.One / new Vector2(Width, Height));

                _combined.Bind(0);  // Read from attachment 2, write to attachment 1
            }
        });

        _passes.Add(new StagePass<LitStageContext>("Shadow Viz Pass", Generate("Framebuffers/shadow.frag"), _shadowViz)
        {
            SetupBindings = (ctx, shader) =>
            {
                var cascadeCount = ctx.Shadows?.CascadeCount ?? 1;
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

        _passes.Add(new StagePass<ClusterDebugStageContext>("Cluster Viz Pass", Generate("Framebuffers/cluster_debug.frag", ClusteredLightSystem.LightingDefines), _clusterViz)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("gPosition", 0);
                shader.SetUniform("sceneTexture", 1);

                ctx.Geometry.Bind(EDeferredTexture.Position, 0);
                (ctx.AntiAliasing ? _fxaa : _combined).Bind(1);

                shader.SetUniform("uZNear", ctx.Camera.NearClipPlane);
                shader.SetUniform("uZFar", ctx.Camera.FarClipPlane);
                shader.SetUniform("uMode", ctx.Mode);
                shader.SetUniform("uOverlay", ctx.Overlay);
                shader.SetUniform("uShowGrid", ctx.ShowGrid);
                shader.SetUniform("uMaxLightsPerCluster", ClusteredLightSystem.MaxLightsPerClusterLimit);

                if (ctx.LightSystem is { IsEnabled: true } system)
                {
                    system.BindForRendering();
                    shader.SetUniform("uHasLights", true);
                    shader.SetUniform("uGridDimX", system.GridDimensionX);
                    shader.SetUniform("uGridDimY", system.GridDimensionY);
                    shader.SetUniform("uGridDimZ", system.GridDimensionZ);
                }
                else shader.SetUniform("uHasLights", false);
            }
        });

        _passes.Add(new StagePass<FinalStageContext>("Final Pass", Generate("Framebuffers/final.frag"))
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("texture1", 0);
                shader.SetUniform("texture2", 1);
                shader.SetUniform("enabled", ctx.Texture != null);
                shader.SetUniform("split", ctx.Split ?? 1.0f);
                shader.SetUniform("channel", ctx.Channel);

                (ctx.AntiAliasing ? _fxaa : _combined).Bind(0);
                ctx.Texture?.Bind(1);
            }
        });

        _passes.Add(new StagePass<GeometryStageContext>("Picking Pass", Generate("Picking/combine.frag"), _picking)
        {
            SetupBindings = (ctx, shader) =>
            {
                shader.SetUniform("deferredPicking", 0);
                shader.SetUniform("forwardPicking", 1);

                ctx.Geometry.Bind(EDeferredTexture.Picking, 0);
                ctx.Geometry.Bind(EForwardTexture.Picking, 1);
            }
        });

        _passes.Add(new StagePass<NoStageContext>("Picking Viz Pass", Generate("Picking/visualize.frag"), _pickingViz)
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
            EPostProcessTexture.ClusterViz => _clusterViz,
            _ => throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid post-process texture type")
        };

        t.Bind(unit);
    }

    public uint GetComponentId(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        var scaleX = windowSize.X / Width;
        var scaleY = windowSize.Y / Height;

        // windowPos is the top left corner of the rendered image, Vector2.Zero when the ui is disabled
        var x = Convert.ToInt32((mousePos.X - windowPos.X) / scaleX);
        var y = Height - 1 - Convert.ToInt32((mousePos.Y - windowPos.Y) / scaleY); // the picking buffer is bottom up, the mouse is top down

        var pixel = _picking.GetPixel<uint>(x, y);
        Log.Verbose("Read pixel at ({X}, {Y}) with scale ({ScaleX}, {ScaleY}), resulting in component ID {ComponentId}", x, y, scaleX, scaleY, pixel);

        return pixel;
    }

    private void Generate(ResizableTexture2D texture, TextureMinFilter filter = TextureMinFilter.Linear)
    {
        texture.Generate();
        texture.Resize(Width, Height);
        GL.TextureParameter(texture, TextureParameterName.TextureMinFilter, (int) filter);
        GL.TextureParameter(texture, TextureParameterName.TextureMagFilter, (int) filter);
        GL.TextureParameter(texture, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TextureParameter(texture, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
    }

    private ShaderProgram Generate(string fragment, string[]? defines = null)
    {
        var shader = new EmbeddedShader("Framebuffers/fullquad.vert", fragment) { Defines = defines };
        shader.Generate();
        shader.Link();
        return shader;
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
        _clusterViz,
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
        _clusterViz.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
        _pickingViz.Resize(newWidth, newHeight);
    }

    public override void Dispose()
    {
        base.Dispose();

        foreach (var pass in _passes)
            pass.Dispose();

        _ssao.Dispose();
        _ssaoBlur.Dispose();
        _lit.Dispose();
        _combined.Dispose();
        _fxaa.Dispose();
        _shadowViz.Dispose();
        _clusterViz.Dispose();
        _picking.Dispose();
        _pickingViz.Dispose();
    }
}
