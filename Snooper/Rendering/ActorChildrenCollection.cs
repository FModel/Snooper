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

    /// <summary>
    /// Inserts without raising <see cref="Actor.OnChildAdded"/>. See <see cref="RemoveQuiet"/>.
    /// </summary>
    internal void AddQuiet(Actor item) => base.InsertItem(Count, item);

    /// <summary>
    /// Removes without raising <see cref="Actor.OnChildRemoved"/>. See <see cref="AddQuiet"/>.
    /// </summary>
    internal bool RemoveQuiet(Actor item)
    {
        var index = IndexOf(item);
        if (index < 0) return false;

        base.RemoveItem(index);
        return true;
    }

    protected override void SetItem(int index, Actor item) => throw new NotSupportedException("Remove then Add: assigning through the indexer skips the lifecycle.");
}
