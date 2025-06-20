using System.Collections.ObjectModel;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering;

public class ActorComponentCollection(Actor actor) : ObservableCollection<ActorComponent>
{
    public Actor Actor { get; } = actor;

    public void Add(params ActorComponent[] components)
    {
        foreach(var component in components)
        {
            base.Add(component);
        }
    }

    protected override void InsertItem(int index, ActorComponent item)
    {
        if (Contains(item))
            return;

        var oldTransformComponent = Actor.Transform;

        base.InsertItem(index, item);

        if (item is TransformComponent && item != oldTransformComponent)
        {
            Remove(oldTransformComponent);
        }
    }
}
