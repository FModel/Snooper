using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SplineMeshRenderSystem : StaticMeshRenderSystem
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

    protected override void OnComponentUpdate(StaticMeshComponent component, float delta)
    {
        if (component is SplineMeshComponent spline && spline.IsDirty(DirtyFlags.Spline))
        {
            if (spline._allocation is null)
            {
                spline._allocation = _params.Add(spline.SplineParams);
                _mapping.Upsert((int) spline.Id, (uint)spline._allocation.Value.StartIndex);
            }
            else
            {
                _params.Update(spline._allocation.Value, spline.SplineParams);
            }
        }

        base.OnComponentUpdate(component, delta);
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _mapping.Bind(8);
        _params.Bind(9);
    }

    private uint _maxComponentId;
    protected override void OnActorComponentEnqueued(StaticMeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component is not SplineMeshComponent spline) return;

        if (spline.Id > _maxComponentId)
        {
            _maxComponentId = spline.Id;
        }
    }

    public override bool Accepts(Type type) => type == typeof(SplineMeshComponent);

    protected override bool CanEnqueueActorComponent(StaticMeshComponent component) => true;

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
