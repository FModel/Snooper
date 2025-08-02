using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerDrawData>
    : ActorSystem<TComponent>, IMemorySizeProvider
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerDrawData : unmanaged, IPerDrawData
{
    public override uint Order => 19;
    protected override bool AllowDerivation => false;
    
    protected abstract Action<ArrayBuffer<TVertex>> PointersFactory { get; }

    protected readonly IndirectResources<TVertex, TInstanceData, TPerDrawData> Resources;
    protected readonly TextureManager TextureManager;

    protected IndirectRenderSystem(int initialDrawCapacity, PrimitiveType type)
    {
        Resources = new IndirectResources<TVertex, TInstanceData, TPerDrawData>(initialDrawCapacity, type);
        
        TextureManager = new TextureManager();
        TextureManager.OnSectionReady += section =>
        {
            // this is called when a managed texture has been decoded (async) and uploaded to the GPU
            // it gives back the bindless representation of the texture for TPerDrawData to use
            // at this point, TPerDrawData is still defaulted
            
            section.DrawDataContainer?.FinalizeGpuData();
            if (section.DrawDataContainer?.Raw is not TPerDrawData raw)
            {
                throw new InvalidOperationException($"Draw data container raw type {section.DrawDataContainer.Raw.GetType()} does not match expected type {typeof(TPerDrawData)}.");
            }
            
            Resources.Update(section.DrawMetadata.DrawId, raw);
        };
    }

    public override void Load()
    {
        base.Load();

        Resources.Generate();
        Resources.Bind();
        Resources.Allocate(_componentCount, _drawCount, _indices, _vertices);
        
        foreach (var component in Components)
        {
            component.Generate(Resources, TextureManager);
        }
        PointersFactory(Resources.VBO);
        
        Resources.Unbind();
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        
        // dequeue textures
        TextureManager.Update(delta);

        Resources.Bind();
        foreach (var component in Components)
        {
            component.Update(Resources, TextureManager);
        }
        Resources.Unbind();
    }

    public override void Render(CameraComponent camera)
    {
        Resources.Render();
    }
    
    private int _componentCount;
    private int _drawCount;
    private int _indices;
    private int _vertices;

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);
        
        _componentCount++;
        foreach (var lod in component.LevelOfDetails)
        {
            _drawCount += lod.SectionDescriptors.Length;
            _indices += lod.Primitive.Indices.Length;
            _vertices += lod.Primitive.Vertices.Length;
        }
    }

    protected override void OnActorComponentRemoved(TComponent component)
    {
        base.OnActorComponentRemoved(component);

        foreach (var section in component.Sections)
            Resources.Remove(section.DrawMetadata);
    }
    
    public override void Dispose()
    {
        base.Dispose();
        Resources.Dispose();
        TextureManager.Dispose();
    }

    public string GetFormattedSpace() => Resources.GetFormattedSpace();
}
