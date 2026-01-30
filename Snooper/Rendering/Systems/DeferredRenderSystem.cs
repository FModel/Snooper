using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem, IShadowSupportedSystem
{
    public override uint Order => 23;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;

    private readonly ShaderProgram _shadowShader = new EmbeddedShader("Shadows/shadow_cascade.vert", "empty.frag")
    {
        Geometry = "Shadows/shadow_cascade.geom"
    };

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

    public void RenderShadows(IViewProjectionProvider[] cascades)
    {
        if (IsCulled)
            Resources.Cull(cascades[^1]); // use the farthest cascade camera for culling

        _shadowShader.Use();
        for (int i = 0; i < cascades.Length; i++)
        {
            _shadowShader.SetUniform($"uViewMatrices[{i}]", cascades[i].ViewMatrix);
            _shadowShader.SetUniform($"uProjectionMatrices[{i}]", cascades[i].ProjectionMatrix);
        }

        Resources.Render();
    }
}
