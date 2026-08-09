using System.Collections.ObjectModel;
using Snooper.Rendering.Actors;

namespace Snooper.Rendering;

public class ActorChildrenCollection(Actor actor) : Collection<Actor>
{
    protected override void InsertItem(int index, Actor item)
    {
        base.InsertItem(index, item);
        actor.OnChildAdded(item);
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];

        base.RemoveItem(index);
        actor.OnChildRemoved(item);
    }

    protected override void ClearItems()
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            RemoveAt(i);
        }
    }

    protected override void SetItem(int index, Actor item) => throw new NotSupportedException("Remove then Add: assigning through the indexer skips the lifecycle.");
}
