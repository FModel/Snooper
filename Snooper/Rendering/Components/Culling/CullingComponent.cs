using System.Numerics;
using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Culling;

[DefaultActorSystem(typeof(CullingSystem))]
public abstract class CullingComponent : ActorComponent
{
    public abstract void Update(CameraComponent cameraComponent, Plane[] frustum);
    public abstract float GetScreenSpaceCoverage(CameraComponent camera);
}
