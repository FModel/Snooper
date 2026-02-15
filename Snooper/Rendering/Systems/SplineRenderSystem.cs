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

    private readonly ShaderStorageBuffer<uint> _mapping = new();
    private readonly ShaderStorageBuffer<SplineMeshParams> _params = new();

    protected override void OnLoad()
    {
        foreach (var shader in Shaders.Values)
        {
            shader.Vertex = "spline.vert";
        }

        base.OnLoad();

        var splines = Components.OfType<SplineMeshComponent>().ToArray();

        var maxComponentId = 0u;
        foreach (var component in splines)
        {
            if (component.Id > maxComponentId)
                maxComponentId = component.Id;
        }

        _mapping.Generate();
        _mapping.Allocate(maxComponentId + 1);

        _params.Generate();
        _params.Allocate(ComponentsCount);

        foreach (var component in splines)
        {
            _mapping.Upsert((int) component.Id, (uint)_params.Add(component.SplineParams).StartIndex);
        }
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _mapping.Bind(3);
        _params.Bind(4);
    }

    public override bool Accepts(Type type) => type == typeof(SplineMeshComponent);

    protected override bool CanEnqueueActorComponent(MeshComponent component) => true;

    public override long Allocated => base.Allocated + _mapping.Allocated + _params.Allocated;
    public override long Used => base.Used + _mapping.Used + _params.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Mapping Buffer", _mapping);
        yield return new MemoryDetail("Params Buffer", _params);
    }
}
