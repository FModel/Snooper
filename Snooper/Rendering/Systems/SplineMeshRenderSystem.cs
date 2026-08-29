using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SplineMeshRenderSystem() : MeshRenderSystem<SplineMeshComponent>(["SPLINE_VERTEX", ..SplineBindings.OwnDefines], 1)
{
    private abstract class SplineBindings : Bindings
    {
        public const uint Params = BaseMaxBinding + 1;
        public const uint MaxBinding = Params;

        public static readonly string[] OwnDefines =
        [
            Define("SPLINE_PARAMS", Params)
        ];
    }

    public override uint Order => 24;
    public override uint? MaxBindingUsed => SplineBindings.MaxBinding;
    protected override bool IsCulled => false; // TODO: alter the bounding box based on the spline params, then restore culling, then remove the view count of 1

    private readonly ShaderStorageBuffer<SplineMeshParams> _params = new();
    protected override IEnumerable<(uint, IIndexedBind)> SystemBuffers =>
    [
        (SplineBindings.Params, _params)
    ];

    protected override void OnLoad()
    {
        base.OnLoad();

        _params.Generate();
        if (Counts.Instances > 0) _params.Allocate(Counts.Instances);
    }

    protected override void OnResourcesAdded(SplineMeshComponent component, ResourcesMetadata metadata)
    {
        base.OnResourcesAdded(component, metadata);

        UploadParams(component, metadata);
    }

    protected override void OnComponentUpdate(SplineMeshComponent component, float delta)
    {
        // the first upload is done by OnResourcesAdded, the only point where the instances to write to are known
        if (component.IsDirty(DirtyFlags.Spline) && component.Metadata is { } metadata)
        {
            UploadParams(component, metadata);
            component.MarkClean(DirtyFlags.Spline);
        }

        base.OnComponentUpdate(component, delta);
    }

    private void UploadParams(SplineMeshComponent component, ResourcesMetadata metadata)
    {
        // one set of params per component, so every instance of it gets the same entry
        for (var i = 0; i < metadata.InstanceAllocation.Length; i++)
        {
            _params.Upsert(metadata.InstanceAllocation.StartIndex + i, component.SplineParams);
        }
    }

    public override long Allocated => base.Allocated + _params.Allocated;
    public override long Used => base.Used + _params.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        yield return new MemoryDetail("Params Buffer", _params);
    }
}
