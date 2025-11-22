using System.Collections.ObjectModel;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.Rendering;

public class ActorComponentCollection(Actor actor) : ObservableCollection<ActorComponent>
{
    public Actor Actor { get; } = actor;
}
