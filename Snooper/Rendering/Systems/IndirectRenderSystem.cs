using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerDrawData>
    : ActorSystem<TComponent>, ITexturedSystem, IMemorySizeProvider
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerDrawData : unmanaged, IPerDrawData
{
    public override uint Order => 19;
    protected override bool AllowDerivation => false;
    
    protected abstract Action<int> VertexLayout { get; }

    protected IndirectResources<TVertex, TInstanceData, TPerDrawData> Resources { get; }
    public TextureManager TextureManager { get; }

    protected IndirectRenderSystem(int initialDrawCapacity, PrimitiveType type)
    {
        Resources = new IndirectResources<TVertex, TInstanceData, TPerDrawData>(initialDrawCapacity, type);
        
        TextureManager = new TextureManager();
        TextureManager.OnMaterialReady += material =>
        {
            // this is called when a managed texture has been decoded (async) and uploaded to the GPU
            // it gives back the bindless representation of the texture for TPerDrawData to use
            // at this point, TPerDrawData is still defaulted
            
            material.DrawDataContainer?.FinalizeGpuData();
            if (material.DrawDataContainer?.Raw is not TPerDrawData raw)
            {
                throw new InvalidOperationException($"Draw data container raw type {material.DrawDataContainer.Raw.GetType()} does not match expected type {typeof(TPerDrawData)}.");
            }
            
            Resources.Update(material.DrawMetadata.DrawId, raw);
            material.DrawDataContainer?.Dispose();
        };
    }

    public override void Load()
    {
        base.Load();

        Resources.Generate();
        Resources.Allocate(_componentCount, _drawCount, _indices, _vertices);
        
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
    private uint _indices;
    private uint _vertices;
    private readonly HashSet<FGuid> _guids = [];

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);
        
        _componentCount++;
        _drawCount += (uint)component.Descriptor.Lods[0].Sections.Length;
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

        foreach (var material in component.Materials)
        {
            if (!material.IsGenerated) continue;
            Resources.Remove(material.DrawMetadata);
        }
    }
    
    public override void Dispose()
    {
        base.Dispose();
        Resources.Dispose();
        TextureManager.Dispose();
    }

    public string GetFormattedSpace() => Resources.GetFormattedSpace();
}
