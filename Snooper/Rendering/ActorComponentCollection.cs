using System.Collections.ObjectModel;
using Snooper.Rendering.Actors;

namespace Snooper.Rendering;

public class ActorComponentCollection(Actor actor) : ObservableCollection<ActorComponent>
{
    public Actor Actor { get; } = actor;

    public void AddRange(params ActorComponent[] components)
    {
        foreach(var component in components)
        {
            base.Add(component);
        }
    }
}
