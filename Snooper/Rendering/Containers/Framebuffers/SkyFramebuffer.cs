using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Skybox;

namespace Snooper.Rendering.Containers.Framebuffers;

public readonly record struct SkyParameters
{
    private const float Intensity = 56.0f; // because we don't auto exposure

    public readonly Vector3 SunDirection;
    public readonly Vector3 SunLuminance;
    public readonly float PlanetRadius;
    public readonly float AtmosphereHeight;
    public readonly float Altitude;
    public readonly Vector3 RayleighScattering;
    public readonly float RayleighScaleHeight;
    public readonly float MieScattering;
    public readonly float MieAbsorption;
    public readonly float MieScaleHeight;
    public readonly float MieAnisotropy;
    public readonly Vector3 OzoneAbsorption;

    public SkyParameters(SkyAtmosphereComponent sky, Vector3 sunDirection, Vector3 sunColor, float viewerHeight)
    {
        SunDirection = sunDirection;
        SunLuminance = sunColor * sky.LuminanceFactor * Intensity;
        PlanetRadius = sky.PlanetRadius;
        AtmosphereHeight = sky.AtmosphereHeight;
        Altitude = MathF.Max(MathF.Round((viewerHeight - sky.PlanetTop) / 10.0f) * 10.0f, 100.0f);
        RayleighScattering = sky.RayleighScattering;
        RayleighScaleHeight = sky.RayleighScaleHeight;
        MieScattering = sky.MieScattering;
        MieAbsorption = sky.MieAbsorption;
        MieScaleHeight = sky.MieScaleHeight;
        MieAnisotropy = sky.MieAnisotropy;
        OzoneAbsorption = sky.OzoneAbsorption;
    }
}

/// <summary>
/// the sky baked into a small texture, so the skybox reads it instead of marching the atmosphere per pixel
/// </summary>
public class SkyFramebuffer() : FullQuadFramebuffer<ESkyTexture>(256, 128, SizedInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat)
{
    private readonly ShaderProgram _shader = new EmbeddedShader("Framebuffers/fullquad.vert", "Skybox/sky_lut.frag");
    public SkyParameters Baked { get; private set; }

    public override void Generate()
    {
        base.Generate();

        Color.SetSampling(TextureMinFilter.Linear, TextureMagFilter.Linear, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge);

        _shader.Generate();
        _shader.Link();
    }

    public void Bake(SkyAtmosphereComponent atmosphere, Vector3 sunDirection, Vector3 sunColor, float viewerHeight)
    {
        var sky = new SkyParameters(atmosphere, sunDirection, sunColor, viewerHeight);
        if (Baked == sky) return;

        var viewport = new int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        var program = GL.GetInteger(GetPName.CurrentProgram);
        var blend = GL.IsEnabled(EnableCap.Blend);
        var cull = GL.IsEnabled(EnableCap.CullFace);

        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.CullFace);

        Bind();
        Render(() =>
        {
            _shader.Use();
            _shader.SetUniform("uSunPos", sky.SunDirection);
            _shader.SetUniform("uSunLuminance", sky.SunLuminance);
            _shader.SetUniform("uPlanetRadius", sky.PlanetRadius);
            _shader.SetUniform("uAtmosphereHeight", sky.AtmosphereHeight);
            _shader.SetUniform("uAltitude", sky.Altitude);
            _shader.SetUniform("uRayleighScattering", sky.RayleighScattering);
            _shader.SetUniform("uRayleighScaleHeight", sky.RayleighScaleHeight);
            _shader.SetUniform("uMieScattering", sky.MieScattering);
            _shader.SetUniform("uMieAbsorption", sky.MieAbsorption);
            _shader.SetUniform("uMieScaleHeight", sky.MieScaleHeight);
            _shader.SetUniform("uMieAnisotropy", sky.MieAnisotropy);
            _shader.SetUniform("uOzoneAbsorption", sky.OzoneAbsorption);
        });
        GL.UseProgram(program);
        Unbind();

        if (blend) GL.Enable(EnableCap.Blend);
        if (cull) GL.Enable(EnableCap.CullFace);
        GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);

        Baked = sky;
    }

    public override void Bind(ESkyTexture texture, uint unit)
    {
        if (texture != ESkyTexture.Color)
            throw new ArgumentOutOfRangeException(nameof(texture), texture, "Invalid sky texture type");

        Color.Bind(unit);
    }

    public override long Allocated => base.Allocated + _shader.Allocated;
    public override long Used => base.Used + _shader.Used;

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Bake Shader", _shader);
    }

    public override void Dispose()
    {
        base.Dispose();

        _shader.Dispose();
    }
}
