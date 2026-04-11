using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SplineMeshRenderSystem() : MeshRenderSystem<SplineMeshComponent>(["SPLINE_VERTEX"])
{
    public override uint Order => 24;
    protected override bool IsCulled => false; // TODO: alter the bounding box based on the spline params, then restore culling

    private readonly ShaderStorageBuffer<uint> _mapping = new();
    private readonly ShaderStorageBuffer<SplineMeshParams> _params = new();

    protected override void OnLoad()
    {
        base.OnLoad();

        _mapping.Generate();
        _mapping.Allocate(_maxComponentId + 1);

        _params.Generate();
        _params.Allocate(EnqueuedComponentsCount);
    }

    protected override void OnComponentUpdate(SplineMeshComponent component, float delta)
    {
        if (component.IsDirty(DirtyFlags.Spline))
        {
            if (component._allocation is null)
            {
                component._allocation = _params.Add(component.SplineParams);
                _mapping.Upsert(component.Id, (uint)component._allocation.Value.StartIndex);
            }
            else
            {
                _params.Update(component._allocation.Value, component.SplineParams);
            }

            component.MarkClean(DirtyFlags.Spline);
        }

        base.OnComponentUpdate(component, delta);
    }

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        _mapping.Bind(8);
        _params.Bind(9);
    }

    private int _maxComponentId;
    protected override void OnActorComponentEnqueued(SplineMeshComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (component.Id > _maxComponentId)
        {
            _maxComponentId = component.Id;
        }
    }

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
