using CUE4Parse.UE4.Objects.Core.Math;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerDrawData>
    : ActorSystem<TComponent>, IMemorySizeProvider
    where TVertex : unmanaged
    where TComponent : TPrimitiveComponent<TVertex, TInstanceData, TPerDrawData>
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
            Resources.Update(section.DrawMetadata.DrawId, (TPerDrawData)section.DrawDataContainer.Raw); // TODO: watch out for casting issues
        };
    }

    public override void Load()
    {
        base.Load();
        
        var allocation = 0;

        // generate draw metadata for all components + add all textures to the texture manager
        Resources.Generate();
        Resources.Bind();
        foreach (var component in Components)
        {
            component.Generate(Resources);
            allocation += component.Sections.Length;
            
            TextureManager.AddRange(component.Sections);
        }
        PointersFactory(Resources.VBO);
        Resources.Unbind();

        // allocate draw data for all sections (empty at this point)
        if (allocation > 0) Resources.AllocateDrawData(allocation);
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        
        // dequeue textures
        TextureManager.Update(delta);

        Resources.Bind();
        foreach (var component in Components)
        {
            component.Update(Resources);
        }
        Resources.Unbind();
    }

    public override void Render(CameraComponent camera)
    {
        Resources.Render();
    }

    protected override void OnActorComponentRemoved(TComponent component)
    {
        base.OnActorComponentRemoved(component);

        foreach (var section in component.Sections)
            Resources.Remove(section.DrawMetadata);
    }

    public string GetFormattedSpace() => Resources.GetFormattedSpace();
}
