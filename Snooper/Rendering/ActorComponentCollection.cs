using System.Collections.ObjectModel;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.Rendering;

public class ActorComponentCollection(Actor actor) : Collection<ActorComponent>
{
    protected override void InsertItem(int index, ActorComponent item)
    {
        base.InsertItem(index, item);
        actor.OnComponentAdded(item);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];

        base.RemoveItem(index);
        actor.OnComponentRemoved(item);
    }

    protected override void ClearItems()
    {
        // one at a time, so each of them actually ends play
        for (var i = Count - 1; i >= 0; i--)
        {
            RemoveAt(i);
        }
    }

    protected override void SetItem(int index, ActorComponent item) => throw new NotSupportedException("Remove then Add: assigning through the indexer skips the lifecycle.");
}
