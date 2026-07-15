using System.Collections.Concurrent;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type) : ActorSystem<TComponent>, IMemoryDetailsProvider
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint? MaxBindingUsed => Bindings.BaseMaxBinding;
    public override ActorSystemType SystemType => ActorSystemType.Rendering;
    protected override bool AllowDerivation => false;

    protected abstract Action<uint> VertexLayout { get; }

    protected IndirectResources<TVertex, TInstanceData, TPerMaterialData> Resources { get; } = new(type);

    protected override void OnLoad()
    {
        base.OnLoad();

        Resources.Generate();
        Resources.Allocate(_counts, DisplayName);

        Resources.SetVertexLayout(VertexLayout);
    }

    protected override void OnUpdate(float delta)
    {
        base.OnUpdate(delta);

        Resources.Flush();
    }

    protected override void PreOnUpdate(TComponent[] components)
    {
        base.PreOnUpdate(components);

        if (ClearMaskBuffer)
            Resources.ClearMaskBuffer();

        Resources.BeginDeferMerge();
    }

    protected override void OnComponentUpdate(TComponent component, float delta)
    {
        // TODO: that's ugly
        var isNew = component.Metadata is null;
        component.Update(Resources);
        if (isNew && component.Metadata is { } metadata)
        {
            OnResourcesAdded(component, metadata);
        }
    }

    /// <summary>
    /// Called once per component, right after its GPU resources have been created.
    /// Lets derived systems upload their own per-component/per-mesh data (e.g. skinning buffers).
    /// TODO: improve and leverage
    /// </summary>
    protected virtual void OnResourcesAdded(TComponent component, ResourcesMetadata metadata)
    {
    }

    protected override void PostOnUpdate()
    {
        base.PostOnUpdate();

        Resources.EndDeferMerge();
    }

    protected override void OnRender(CameraComponent camera, CommandBufferType type)
    {
        Resources.Render(type);
    }

    private AllocationCounts _counts;
    private readonly ConcurrentDictionary<FGuid, byte> _guids = [];

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);

        _counts.Components++;
        _counts.Instances += component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        if (component.Descriptor.Lods.Length > 0)
            _counts.Draws += (uint)component.Descriptor.Lods[0].Sections.Length;
        _counts.Materials += (uint)component.Materials.Length;
        if (_guids.TryAdd(component.Descriptor.Guid, 0))
        {
            _counts.UniqueComponents++;

            foreach (var lod in component.Descriptor.Lods)
            {
                _counts.Sections += (uint)lod.Sections.Length;
                _counts.Indices += lod.IndexCount;
                _counts.Vertices += lod.VertexCount;

                if (lod.HasColoredVertices) _counts.ColoredVertices += lod.VertexCount;
            }
        }
    }

    protected override void OnActorComponentRemoved(TComponent component)
    {
        base.OnActorComponentRemoved(component);

        // not used ig
        _counts.Components--;
        _counts.Instances -= component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        if (component.Descriptor.Lods.Length > 0)
            _counts.Draws -= (uint)component.Descriptor.Lods[0].Sections.Length;
        _counts.Materials -= (uint)component.Materials.Length;
        if (_guids.Remove(component.Descriptor.Guid, out _))
        {
            _counts.UniqueComponents--;

            foreach (var lod in component.Descriptor.Lods)
            {
                _counts.Indices -= lod.IndexCount;
                _counts.Vertices -= lod.VertexCount;

                if (lod.HasColoredVertices) _counts.ColoredVertices -= lod.VertexCount;
            }
        }

        Resources.Remove(component);
    }

    public override void Dispose()
    {
        Resources.Dispose();

        base.Dispose();
    }

    public virtual long Allocated => Resources.Allocated;
    public virtual long Used => Resources.Used;

    public virtual IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("GPU Resources", Resources);
    }
}
