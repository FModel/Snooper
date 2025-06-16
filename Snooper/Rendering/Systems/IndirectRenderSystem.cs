using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class IndirectRenderSystem<TVertex, TComponent>(int initialDrawCapacity) : ActorSystem<TComponent>, IMemorySizeProvider where TComponent : TPrimitiveComponent<TVertex> where TVertex : unmanaged
{
    public override uint Order => 19;
    protected override bool AllowDerivation => false;

    protected readonly IndirectResources<TVertex> Resources = new(initialDrawCapacity);
    protected abstract Action<ArrayBuffer<TVertex>> PointersFactory { get; }

    public override void Load()
    {
        base.Load();

        Resources.Generate();
        Resources.Bind();
        foreach (var component in Components)
        {
            component.Generate(Resources);
        }
        PointersFactory(Resources.VBO);
        Resources.Unbind();
    }

    public override void Update(float delta)
    {
        base.Update(delta);

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

        Resources.Remove(component.DrawId);
    }

    public string GetFormattedSpace() => Resources.GetFormattedSpace();
}
