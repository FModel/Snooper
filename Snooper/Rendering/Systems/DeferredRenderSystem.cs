using Snooper.Core.Systems;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem
{
    public override uint Order => 23;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    
    public override void Load()
    {
        Shader.Fragment = "geometry.frag";
        
        base.Load();
    }

    protected override bool CanEnqueueActorComponent(MeshComponent component)
    {
        return component is { IsTranslucent: false, IsVisible: true };
    }
}
