using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>
    : ActorSystem<TComponent>, ITexturedSystem, IMemorySizeProvider
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint Order => 19;
    protected override bool AllowDerivation => false;
    
    protected abstract Action<int> VertexLayout { get; }

    protected IndirectResources<TVertex, TInstanceData, TPerMaterialData> Resources { get; }
    public TextureManager TextureManager { get; }

    protected IndirectRenderSystem(int initialDrawCapacity, PrimitiveType type)
    {
        Resources = new IndirectResources<TVertex, TInstanceData, TPerMaterialData>(initialDrawCapacity, type);
        
        TextureManager = new TextureManager();
        TextureManager.OnMaterialReady += material =>
        {
            material.MaterialDataContainer?.FinalizeGpuData();
            if (material.MaterialDataContainer?.Raw is not TPerMaterialData raw)
            {
                throw new InvalidOperationException($"Material data container raw type {material.MaterialDataContainer.Raw.GetType()} does not match expected type {typeof(TPerMaterialData)}.");
            }
            
            Resources.Update((int)material.MaterialOffset, raw);
            material.MaterialDataContainer?.Dispose();
        };
    }

    public override void Load()
    {
        base.Load();

        Resources.Generate();
        Resources.Allocate(_componentCount, _drawCount, _materialCount, _indices, _vertices);
        
        TextureManager.Load();
        
        foreach (var component in Components)
        {
            component.Generate(Resources, TextureManager);
        }
        Resources.SetVertexLayout(VertexLayout);
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        
        // dequeue textures
        TextureManager.Update(delta);

        foreach (var component in Components)
        {
            component.Update(Resources, TextureManager);
        }
    }

    public override void Render(CameraComponent camera)
    {
        Resources.Render();
    }
    
    private uint _componentCount;
    private uint _drawCount;
    private uint _materialCount;
    private uint _indices;
    private uint _vertices;
    private readonly HashSet<FGuid> _guids = [];

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);
        
        _componentCount++;
        _drawCount += (uint)component.Descriptor.Lods[0].Sections.Length;
        _materialCount += (uint)component.Materials.Length;
        if (_guids.Add(component.Descriptor.Guid))
        {
            foreach (var lod in component.Descriptor.Lods)
            {
                _indices += lod.IndexCount;
                _vertices += lod.VertexCount;
            }
        }
    }

    protected override void OnActorComponentRemoved(TComponent component)
    {
        base.OnActorComponentRemoved(component);

        Resources.Remove(component.Metadata);
    }
    
    public override void Dispose()
    {
        base.Dispose();
        Resources.Dispose();
        TextureManager.Dispose();
    }

    public string GetFormattedSpace() => Resources.GetFormattedSpace();
}
