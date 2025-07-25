using System.Numerics;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Culling;

namespace Snooper.Rendering.Systems;

public class CullingSystem : ActorSystem<CullingComponent>
{
    public override uint Order => 11;
    protected override bool AllowDerivation => true;

    public override void Render(CameraComponent camera)
    {
        if (!camera.IsActive) return;
        // TODO: do this OnUpdateFrame instead
        
        var frustum = camera.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }
        
        Parallel.ForEach(Components, component => component.CheckForVisibility(frustum));
    }

    protected override void OnActorComponentEnqueued(CullingComponent component)
    {
        base.OnActorComponentEnqueued(component);

        switch (component)
        {
            case BoxCullingComponent box:
            {
                Vector3? color = null;
                if (component.Actor?.Parent is { Parent: not null }) // just an example
                {
                    var id = component.Actor.Parent.Id;
                    color = new Vector3((id & 0xFF) / 255f, ((id >> 8) & 0xFF) / 255f, ((id >> 16) & 0xFF) / 255f);
                }
                
                component.Actor?.Components.Add(new DebugComponent(box, color));
                break;
            }
        }
    }
}
