using System.Numerics;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Managers;

public interface IStageContext;

public sealed record NoStageContext : IStageContext;

public sealed record GeometryStageContext(
    GeometryRenderer Geometry
) : IStageContext;

public sealed record AmbientOcclusionStageContext(
    CameraComponent Camera,
    GeometryRenderer Geometry,
    int DirectionCount = 6,
    int StepsPerDirection = 6
) : IStageContext;

public sealed record LitStageContext(
    CameraComponent Camera,
    GeometryRenderer Geometry,
    ClusteredLightSystem? LightSystem,
    bool AmbienOcclusion = true,
    ShadowStageContext? ShadowContext = null
) : IStageContext;

public sealed record ShadowStageContext(
    int Width,
    int Height,
    int Depth,
    float Bias,
    float[] PlaneDistances,
    Matrix4x4[] Matrices
) : IStageContext;
