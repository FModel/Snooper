using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem, IShadowSupportedSystem
{
    public override uint Order => 23;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;

    private readonly ShaderProgram _shadowShader = new EmbeddedShader("default.vert", "empty.frag");

    protected override void OnLoad()
    {
        Shader.Fragment = "geometry.frag";

        _shadowShader.Generate();
        _shadowShader.Link();

        base.OnLoad();
    }

    protected override bool CanEnqueueActorComponent(MeshComponent component)
    {
        return component is { IsOpaque: true };
    }

    public void RenderShadows(CameraComponent light)
    {
        PreRender(light, _shadowShader);
        OnRender(light);
        PostRender(light, _shadowShader);
    }
}
