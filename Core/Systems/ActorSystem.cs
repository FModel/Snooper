using Snooper.Rendering;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Core.Systems;

public abstract class ActorSystem(Type? componentType) : IGameSystem
{
    public Type? ComponentType { get; } = componentType;
    public ActorManager? ActorManager { get; internal set; }

    public abstract void Load();
    public abstract void Update(float delta);
    public abstract void Render(CameraComponent camera);

    public abstract void ProcessActorComponent(ActorComponent component, Actor actor);
    public virtual bool Accepts(Type type) => ComponentType?.IsAssignableFrom(type) ?? false;

    public void Dispose()
    {

    }
}

public abstract class ActorSystem<TComponent>() : ActorSystem(typeof(TComponent)) where TComponent : ActorComponent
{
    protected HashSet<TComponent> Components { get; } = [];

    public override void ProcessActorComponent(ActorComponent component, Actor actor)
    {
        if (component is not TComponent actorComponent)
            throw new ArgumentException("The actor component must be assignable to TComponent", nameof(component));

        switch (Components.Contains(actorComponent))
        {
            case false:
                Components.Add(actorComponent);
                OnActorComponentAdded(actorComponent);
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
}
