using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Managers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData> : ActorSystem<TComponent>, ITexturedSystem
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint Order => 19;
    protected override bool AllowDerivation => false;

    protected abstract Action<uint> VertexLayout { get; }

    protected IndirectResources<TVertex, TInstanceData, TPerMaterialData> Resources { get; }
    public TextureManager TextureManager { get; } // TODO: make it shared bruh no need one per system

    protected IndirectRenderSystem(PrimitiveType type)
    {
        Resources = new IndirectResources<TVertex, TInstanceData, TPerMaterialData>(type);

        TextureManager = new TextureManager();
        TextureManager.OnMaterialReady += Resources.Update;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        Resources.Generate();
        Resources.Allocate(_counts);

        TextureManager.Load();

        foreach (var component in Components)
        {
            component.Generate(Resources, TextureManager);
        }
        Resources.SetVertexLayout(VertexLayout);
    }

    protected override void OnUpdate(float delta)
    {
        // dequeue textures
        TextureManager.Update(delta);

        base.OnUpdate(delta);

        Resources.FlushUpdates();
    }

    protected override void OnComponentUpdate(TComponent component, float delta)
    {
        component.Update(Resources, TextureManager);
    }

    protected override void OnRender(CameraComponent camera)
    {
        Resources.Render();
    }

    private AllocationCounts _counts;
    private readonly HashSet<FGuid> _guids = [];

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);

        _counts.Components++;
        _counts.Instances += component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        _counts.Draws += (uint)component.Descriptor.Lods[0].Sections.Length;
        _counts.Materials += (uint)component.Materials.Length;
        if (_guids.Add(component.Descriptor.Guid))
        {
            _counts.UniqueComponents++;
            foreach (var lod in component.Descriptor.Lods)
            {
                _counts.Sections += (uint)lod.Sections.Length;
                _counts.Indices += lod.IndexCount;
                _counts.Vertices += lod.VertexCount;

                if (lod.HasVertexColors)
                {
                    _counts.ColoredVertices += lod.VertexCount;
                }
            }
        }
    }

    protected override void OnActorComponentRemoved(TComponent component)
    {
        base.OnActorComponentRemoved(component);

        // not used ig
        _counts.Components--;
        _counts.Instances -= component is InstancedStaticMeshComponent i ? (uint)i.LocalInstancedTransforms.Count : 1;
        _counts.Draws -= (uint)component.Descriptor.Lods[0].Sections.Length;
        _counts.Materials -= (uint)component.Materials.Length;
        if (_guids.Remove(component.Descriptor.Guid))
        {
            _counts.UniqueComponents--;
            foreach (var lod in component.Descriptor.Lods)
            {
                _counts.Indices -= lod.IndexCount;
                _counts.Vertices -= lod.VertexCount;

                if (lod.HasVertexColors)
                {
                    _counts.ColoredVertices -= lod.VertexCount;
                }
            }
        }

        Resources.Remove(component);
        foreach (var material in component.Materials)
        {
            material.Dispose();
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        Resources.Dispose();
        TextureManager.Dispose();
    }

    public virtual long Allocated
    {
        get
        {
            long total = 0;
            total += Resources.Allocated;
            total += TextureManager.Allocated;
            return total;
        }
    }

    public virtual long Used
    {
        get
        {
            long total = 0;
            total += Resources.Used;
            total += TextureManager.Used;
            return total;
        }
    }

    public virtual IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("GPU Resources", Resources);
        yield return new MemoryDetail("Texture Manager", TextureManager);
    }
}
