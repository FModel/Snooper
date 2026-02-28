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

        _mapping.Generate();
        _mapping.Allocate(_maxComponentId + 1);

        _params.Generate();
        _params.Allocate(EnqueuedComponentsCount);
    }

    protected override void OnComponentUpdate(MeshComponent component, float delta)
    {
        base.OnComponentUpdate(component, delta);

        if (component is not SplineMeshComponent spline || spline.IsInitialized) return;
        spline.IsInitialized = true;

        _mapping.Upsert((int) spline.Id, (uint)_params.Add(spline.SplineParams).StartIndex);
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _mapping.Bind(3);
        _params.Bind(4);
    }

    private uint _maxComponentId;
    protected override void OnActorComponentEnqueued(MeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component is not SplineMeshComponent spline) return;

        if (spline.Id > _maxComponentId)
        {
            _maxComponentId = spline.Id;
        }
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
