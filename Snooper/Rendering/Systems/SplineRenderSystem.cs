using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SplineRenderSystem : MeshRenderSystem
{
    public override uint Order => 24;
    protected override bool IsCulled => false; // TODO: alter the bounding box based on the spline params, then restore culling

    private readonly ShaderStorageBuffer<SplineMeshParams> _params = new();

    protected override void OnLoad()
    {
        foreach (var shader in Shaders.Values)
        {
            shader.Vertex = "spline.vert";
        }

        base.OnLoad();

        _params.Generate();
        _params.Allocate(ComponentsCount);
        foreach (var component in Components.OfType<SplineMeshComponent>())
        {
            _params.Add(component.SplineParams);
        }
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _params.Bind(3);
    }

    public override bool Accepts(Type type) => type == typeof(SplineMeshComponent);

    protected override bool CanEnqueueActorComponent(MeshComponent component) => true;

    public override long Allocated => base.Allocated + _params.Allocated;
    public override long Used => base.Used + _params.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Params Buffer", _params);
    }
}
