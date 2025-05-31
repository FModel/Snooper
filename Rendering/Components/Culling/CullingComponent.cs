using System.Numerics;
using Snooper.Core;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Culling;

[DefaultActorSystem(typeof(RenderSystem))]
public abstract class CullingComponent(IPrimitiveData primitive) : PrimitiveComponent(primitive)
{
    public bool IsVisible { get; protected set; }
    public Vector3 DebugColor => IsVisible ? new Vector3(0.0f, 1.0f, 0.0f) : new Vector3(1.0f, 0.0f, 0.0f);

    public abstract void Update(CameraComponent cameraComponent);

    public override void Render()
    {
        // if (!IsVisible) return;
        base.Render();
    }
}
