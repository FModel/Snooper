using System.Collections.Concurrent;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Cache;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type, int viewCount = 1) : ActorSystem<TComponent>, IMemoryDetailsProvider, IGeometryRenderSystem
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint? MaxBindingUsed => Bindings.BaseMaxBinding;
    public override ActorSystemType SystemType => ActorSystemType.Rendering;
    protected override bool AllowDerivation => false;

    protected abstract Action<uint> VertexLayout { get; }

    protected IndirectResources<TVertex, TInstanceData, TPerMaterialData> Resources { get; } = new(type, viewCount);
    protected virtual IEnumerable<(uint Binding, IIndexedBind Buffer)> SystemBuffers => [];

    protected override void OnLoad()
    {
        base.OnLoad();

        Resources.Generate();
        Resources.Allocate(Counts);

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
        if (component.Metadata is null)
        {
            var metadata = component.Metadata = Resources.Add(component);

            if (Meshes.TryGetValue(component.Descriptor.Guid, out var entry))
            {
                entry.UploadedBy ??= component.Id;
            }

            OnResourcesAdded(component, metadata);
        }
        else
        {
            Resources.Update(component);
        }
    }

    protected virtual void OnResourcesAdded(TComponent component, ResourcesMetadata metadata)
    {
        // in our current setup, we first upload the geometry data to the GPU and THEN we set up the material sections
        // but it's very much possible here that the material section already has its material data container set
        // in which case, by subscribing to OnMaterialDataContainerSet it will be triggered immediately
        foreach (var material in component.Materials)
        {
            // basically the component is responsible for setting a material container for each section
            // once the container is set, OnMaterialDataContainerSet will be triggered and all the textures in the container will be sent to the texture cache
            // the cache will generate all these textures, make them bindless, finalize the container's GPU data, and then trigger OnContainerReady
            // OnContainerReady will upload the material data to the GPU for this component, in the system that owns it
            // material containers and textures are CPU cached globally, but duplicated on the GPU, per section, per component, per system...

            material.OnContainerReady += Resources.Update;
            material.OnMaterialDataContainerSet += section =>
            {
                TextureCache.Add(section);
                component.IsOpaque &= !section.IsTranslucent;
            };
        }
    }

    protected override void PostOnUpdate()
    {
        base.PostOnUpdate();

        Resources.EndDeferMerge();
    }

    private (uint Binding, IIndexedBind Buffer)[]? _systemBuffers;
    protected void BindSystemBuffers()
    {
        _systemBuffers ??= SystemBuffers.ToArray();
        foreach (var (binding, buffer) in _systemBuffers)
        {
            buffer.Bind(binding);
        }
    }

    public abstract void Cull(ReadOnlySpan<CullView> views);

    public void Render(CameraComponent camera, CommandBufferType type)
    {
        if (!IsEnabled) return;

        using (Scope())
        using (Profiler.Sample(DisplayName))
        {
            if (ShowWireframe) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
            OnRender(camera, type);
            if (ShowWireframe) GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        }
    }

    protected virtual void OnRender(CameraComponent camera, CommandBufferType type)
    {
        Resources.Render(type);
    }

    protected sealed class MeshEntry
    {
        public uint RefCount;
        public int? UploadedBy; // save who actually uploaded the gpu mesh data in GeometryPool
    }
    protected ConcurrentDictionary<FGuid, MeshEntry> Meshes { get; } = [];

    protected AllocationCounts Counts => field ??= CreateCounts();
    protected virtual AllocationCounts CreateCounts() => new();

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);

        Counts.Components++;
        Counts.Instances += component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        if (component.Descriptor.Lods.Length > 0)
            Counts.Draws += (uint)component.Descriptor.Lods[0].Sections.Length;
        Counts.Materials += (uint)component.Materials.Length;

        var entry = Meshes.GetOrAdd(component.Descriptor.Guid, _ => new MeshEntry());
        if (entry.RefCount++ > 0) return;

        // past this point, we know that this is the first time this mesh has been added to the system
        Counts.UniqueComponents++;

        foreach (var lod in component.Descriptor.Lods)
        {
            Counts.Sections += (uint)lod.Sections.Length;
            Counts.Indices += lod.IndexCount;
            Counts.Vertices += lod.VertexCount;

            if (lod.HasColoredVertices) Counts.ColoredVertices += lod.VertexCount;
        }
    }

    protected override void OnActorComponentRemoved(TComponent component, EEndPlayReason reason)
    {
        base.OnActorComponentRemoved(component, reason);

        // not used ig
        Counts.Components--;
        Counts.Instances -= component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        if (component.Descriptor.Lods.Length > 0)
            Counts.Draws -= (uint)component.Descriptor.Lods[0].Sections.Length;
        Counts.Materials -= (uint)component.Materials.Length;

        if (Meshes.TryGetValue(component.Descriptor.Guid, out var entry) && --entry.RefCount == 0)
        {
            Counts.UniqueComponents--;

            foreach (var lod in component.Descriptor.Lods)
            {
                Counts.Sections -= (uint)lod.Sections.Length;
                Counts.Indices -= lod.IndexCount;
                Counts.Vertices -= lod.VertexCount;

                if (lod.HasColoredVertices) Counts.ColoredVertices -= lod.VertexCount;
            }
        }

        // only a component leaving on its own gives its slot back: on a scene swap or a shutdown the
        // whole buffer goes with the system, so freeing slot by slot would be wasted work
        if (reason is EEndPlayReason.Destroyed && component.Metadata != null)
        {
            Resources.Remove(component);
        }
        component.Metadata = null;
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
