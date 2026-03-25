using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class StaticMeshRenderSystem : MeshRenderSystem<StaticMeshComponent>
{
    public override uint Order => 22;

    public override bool Accepts(Type type) => type != typeof(SplineMeshComponent) && base.Accepts(type);
}
