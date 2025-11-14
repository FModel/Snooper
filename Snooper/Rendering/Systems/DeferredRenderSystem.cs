using Snooper.Core.Systems;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem
{
    public override uint Order => 23;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    
    protected override void OnLoad()
    {
        Shader.Fragment = "geometry.frag";
        
        base.OnLoad();
    }

    protected override bool CanEnqueueActorComponent(MeshComponent component)
    {
        return component is { IsOpaque: true, IsVisible: true };
    }
}
