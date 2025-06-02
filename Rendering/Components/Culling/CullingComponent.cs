using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Culling;

[DefaultActorSystem(typeof(RenderSystem))]
public abstract class CullingComponent(IPrimitiveData primitive) : PrimitiveComponent(primitive)
{
    public abstract void Update(CameraComponent cameraComponent);
}
