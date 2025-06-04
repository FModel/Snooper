using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public abstract class ActorSystem(Type? componentType) : IGameSystem
{
    public Type? ComponentType { get; } = componentType;
    public ActorManager? ActorManager { get; internal set; }
    public abstract uint Order { get; }
    public abstract int ComponentsCount { get; }

    public abstract void Load();
    public abstract void Update(float delta);
    public abstract void Render(CameraComponent camera);

    public abstract void ProcessActorComponent(ActorComponent component, Actor actor);

    protected virtual bool AllowDerivation { get => true; }
    public bool Accepts(Type type)
    {
        if (!AllowDerivation) return ComponentType == type;
        return ComponentType?.IsAssignableFrom(type) ?? false;
    }

    protected bool DebugMode => ActorManager?.DebugMode ?? false;

    public void Dispose()
    {

    }
}

public abstract class ActorSystem<TComponent>() : ActorSystem(typeof(TComponent)) where TComponent : ActorComponent
{
    public override int ComponentsCount => Components.Count;
    protected HashSet<TComponent> Components { get; } = [];
    protected Queue<TComponent> ComponentsToLoad { get; } = [];

    public override void Load() => DequeueComponents();
    public override void Update(float delta) => DequeueComponents();

    public override void ProcessActorComponent(ActorComponent component, Actor actor)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));

        switch (Components.Contains(actorComponent))
        {
            case false:
                ComponentsToLoad.Enqueue(actorComponent);
                break;
            case true:
                Components.Remove(actorComponent);
                OnActorComponentRemoved(actorComponent);
                break;
        }
    }

    protected virtual void OnActorComponentAdded(TComponent component)
    {

    }

    protected virtual void OnActorComponentRemoved(TComponent component)
    {

    }
    
    private void DequeueComponents()
    {
        while (ComponentsToLoad.Count > 0)
        {
            var component = ComponentsToLoad.Dequeue();
            Components.Add(component);
            OnActorComponentAdded(component);
        }
    }
}
