using Snooper.Core.Containers.Programs;
using Snooper.Core.Systems;

namespace Snooper.Rendering.Systems;

public class DeferredRenderSystem : RenderSystem
{
    public override uint Order => 23;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("mesh.vert", "geometry.frag");
}
