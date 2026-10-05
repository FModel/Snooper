using System.Numerics;
using System.Runtime.InteropServices;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Skybox;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public sealed class SceneLighting : IMemoryDetailsProvider, IDisposable
{
    [StructLayout(LayoutKind.Sequential, Size = 240)]
    private readonly struct SceneData
    {
        public readonly Matrix4x4 InverseViewMatrix;

        public readonly Vector3 SunDirection;
        public readonly float SunIntensity;
        public readonly Vector3 SunColor;
        public readonly float ZNear;
        public readonly Vector3 SkyLightColor;
        public readonly float ZFar;
        public readonly Vector3 SkyLightLowerColor;
        public readonly float ShadowSoftness;
        public readonly Vector3 CameraPosition;
        public readonly float ShadowNormalOffset;

        public readonly Vector3 FogColor;
        public readonly float FogDensity;
        public readonly Vector3 FogDirectionalColor;
        public readonly float FogDirectionalExponent;
        public readonly float FogHeightFalloff;
        public readonly float FogHeight;
        public readonly float FogMaxOpacity;
        public readonly float FogStartDistance;
        public readonly float FogDirectionalStart;

        public readonly float ShadowBlend;
        public readonly int ShadowCascadeCount;
        public readonly int GridDimX;
        public readonly int GridDimY;
        public readonly int GridDimZ;

        public readonly int UseSunLight;
        public readonly int UseShadows;
        public readonly int UseLighting;
        public readonly int UseFog;

        public SceneData(CameraComponent camera, ClusteredLightSystem? lightSystem, DirectionalLightComponent? sun, ShadowFramebuffer shadows, bool castsShadows, ExponentialHeightFogComponent? fog, SkyLightComponent? skyLight)
        {
            InverseViewMatrix = camera.InverseViewMatrix;
            CameraPosition = camera.InverseViewMatrix.Translation;
            ZNear = camera.NearClipPlane;
            ZFar = camera.FarClipPlane;

            ShadowSoftness = shadows.Softness;
            ShadowNormalOffset = shadows.NormalOffset;
            ShadowBlend = shadows.Blend;
            ShadowCascadeCount = shadows.CascadeCount;

            var daylight = sun?.Daylight ?? 0.0f;
            var (upper, lower) = skyLight != null ? (skyLight.Color * skyLight.Intensity, skyLight.LowerHemisphereColor * skyLight.Intensity) : (Vector3.Zero, Vector3.Zero);
            var fromSky = MathF.PI * Luminance(upper);
            var fromSun = sun != null && daylight > 0.0f ? sun.Intensity / daylight * Luminance(sun.Color) : 0.0f;
            var sunShare = fromSun + fromSky > 0.0f ? fromSun / (fromSun + fromSky) : 1.0f;

            const float budget = 1.25f; // because we don't auto exposure, so the sun and the sky share a budget whatever the values the lights came with

            if (sun != null)
            {
                SunDirection = sun.GetDirection();
                SunColor = sun.Color;
                SunIntensity = MathF.PI * budget * sunShare * daylight; // the diffuse term divides by pi
                UseSunLight = 1;
                UseShadows = castsShadows ? 1 : 0;
            }

            // night mode TODO: through components
            var nightColor = new Vector3(0.080f, 0.100f, 0.145f);
            var nightLowerColor = new Vector3(0.026f, 0.030f, 0.038f);
            const float nightFog = 0.02f;

            var brightest = MathF.Max(upper.X, MathF.Max(upper.Y, upper.Z));
            var skyScale = brightest > 0.0f ? budget * (1.0f - sunShare) / brightest : 0.0f;
            SkyLightColor = Vector3.Lerp(nightColor, upper * skyScale, daylight);
            SkyLightLowerColor = Vector3.Lerp(nightLowerColor, lower * skyScale, daylight);

            if (lightSystem is { UseSceneLights: true, HasClusters: true })
            {
                GridDimX = lightSystem.GridDimensionX;
                GridDimY = lightSystem.GridDimensionY;
                GridDimZ = lightSystem.GridDimensionZ;
                UseLighting = 1;
            }

            if (fog != null)
            {
                FogDensity = fog.Density;
                FogHeightFalloff = fog.HeightFalloff;
                FogHeight = fog.Height;
                FogMaxOpacity = fog.MaxOpacity;
                FogStartDistance = fog.StartDistance;
                FogColor = fog.InscatteringColor * float.Lerp(nightFog, 1.0f, daylight);
                FogDirectionalColor = fog.DirectionalInscatteringColor;
                FogDirectionalExponent = fog.DirectionalInscatteringExponent;
                FogDirectionalStart = fog.DirectionalInscatteringStartDistance;
                UseFog = 1;
            }

            static float Luminance(Vector3 color) => Vector3.Dot(color, new Vector3(0.2126f, 0.7152f, 0.0722f));
        }
    }

    private readonly UniformBuffer<SceneData> _buffer = new();
    private BufferAllocation _allocation;

    private ShadowFramebuffer? _shadows;
    private ClusteredLightSystem? _lightSystem;

    public void Generate()
    {
        _buffer.Generate();
        _allocation = _buffer.Add(default);
    }

    public void Update(CameraComponent camera, ClusteredLightSystem? lightSystem, DirectionalLightComponent? sun, ShadowFramebuffer shadows, bool castsShadows, ExponentialHeightFogComponent? fog, SkyLightComponent? skyLight)
    {
        _shadows = shadows;
        _lightSystem = lightSystem;
        _buffer.Update(_allocation, new SceneData(camera, lightSystem, sun, shadows, castsShadows, fog, skyLight));
    }

    public void Bind()
    {
        _buffer.Bind(Bindings.SceneDataBlock);
        _shadows?.Bind(EShadowTexture.Comparison, Bindings.ShadowMapUnit);
        _shadows?.Bind(EShadowTexture.Views, Bindings.ShadowViewsBlock);
        if (_lightSystem is { UseSceneLights: true, HasClusters: true }) _lightSystem.BindForRendering();
    }

    public long Allocated => _buffer.Allocated;
    public long Used => _buffer.Used;

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Scene Data", _buffer);
    }

    public void Dispose()
    {
        _buffer.Dispose();
    }
}
