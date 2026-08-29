using System.Numerics;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public interface IStageContext;

public readonly struct NoStageContext : IStageContext;

public readonly struct GeometryStageContext(GeometryRenderer geometry) : IStageContext
{
    public readonly GeometryRenderer Geometry = geometry;
}

public readonly struct AmbientOcclusionStageContext(CameraComponent camera, GeometryRenderer geometry, float radius, float intensity, float maxDistance) : IStageContext
{
    public readonly CameraComponent Camera = camera;
    public readonly GeometryRenderer Geometry = geometry;
    public readonly float Radius = radius;
    public readonly float Intensity = intensity;
    public readonly float MaxDistance = maxDistance;
}

public readonly struct BlurStageContext(int radius, GeometryRenderer geometry) : IStageContext
{
    public readonly int Radius = radius;
    public readonly GeometryRenderer Geometry = geometry;
}

public readonly struct LitStageContext(CameraComponent camera, GeometryRenderer geometry, ClusteredLightSystem? lightSystem, bool ambientOcclusion = true, ShadowFramebuffer? shadows = null) : IStageContext
{
    public readonly CameraComponent Camera = camera;
    public readonly GeometryRenderer Geometry = geometry;
    public readonly ClusteredLightSystem? LightSystem = lightSystem;
    public readonly bool AmbientOcclusion = ambientOcclusion;
    public readonly ShadowFramebuffer? Shadows = shadows;
}

public readonly struct ClusterDebugStageContext(CameraComponent camera, GeometryRenderer geometry, ClusteredLightSystem? lightSystem, bool antiAliasing, int mode, float overlay, bool showGrid) : IStageContext
{
    public readonly CameraComponent Camera = camera;
    public readonly GeometryRenderer Geometry = geometry;
    public readonly ClusteredLightSystem? LightSystem = lightSystem;
    public readonly bool AntiAliasing = antiAliasing;
    public readonly int Mode = mode;
    public readonly float Overlay = overlay;
    public readonly bool ShowGrid = showGrid;
}

public readonly struct FinalStageContext(bool antiAliasing, Texture? texture = null, float? split = null, int channel = 0) : IStageContext
{
    public readonly bool AntiAliasing = antiAliasing;
    public readonly Texture? Texture = texture;
    public readonly float? Split = split;
    public readonly int Channel = channel;
}
