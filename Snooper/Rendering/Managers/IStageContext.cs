using System.Numerics;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public interface IStageContext;

public readonly struct NoStageContext : IStageContext;

public readonly struct GeometryStageContext(GeometryRenderer geometry) : IStageContext
{
    public readonly GeometryRenderer Geometry = geometry;
}

public readonly struct AmbientOcclusionStageContext(CameraComponent camera, GeometryRenderer geometry, int directionCount = 6, int stepsPerDirection = 6) : IStageContext
{
    public readonly CameraComponent Camera = camera;
    public readonly GeometryRenderer Geometry = geometry;
    public readonly int DirectionCount = directionCount;
    public readonly int StepsPerDirection = stepsPerDirection;
}

public readonly struct BlurStageContext(int radius) : IStageContext
{
    public readonly int Radius = radius;
}

public readonly struct LitStageContext(CameraComponent camera, GeometryRenderer geometry, ClusteredLightSystem? lightSystem, bool ambientOcclusion = true, ShadowStageContext? shadowContext = null) : IStageContext
{
    public readonly CameraComponent Camera = camera;
    public readonly GeometryRenderer Geometry = geometry;
    public readonly ClusteredLightSystem? LightSystem = lightSystem;
    public readonly bool AmbientOcclusion = ambientOcclusion;
    public readonly ShadowStageContext? ShadowContext = shadowContext;
}

public readonly struct ShadowStageContext(int width, int height, int depth, float bias, float[] planeDistances, Matrix4x4[] matrices) : IStageContext
{
    public readonly int Width = width;
    public readonly int Height = height;
    public readonly int Depth = depth;
    public readonly float Bias = bias;
    public readonly float[] PlaneDistances = planeDistances;
    public readonly Matrix4x4[] Matrices = matrices;
}

public readonly struct FinalStageContext(bool antiAliasing, Texture? texture = null, float? split = null) : IStageContext
{
    public readonly bool AntiAliasing = antiAliasing;
    public readonly Texture? Texture = texture;
    public readonly float? Split = split;
}
