using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering;

/// <summary>
/// custom collection to manage actor children and handle merging of actors with the same GUID
/// this is not what Unreal does, but it helps A LOT with rendering performance on high-actor-count scenes
/// </summary>
public class ActorChildrenCollection : IDictionary<FGuid, Actor>, IEnumerable<Actor>, INotifyCollectionChanged
{
    private readonly Dictionary<FGuid, Actor> _dict = [];
    
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void Add(Actor actor)
    {
        if (_dict.TryGetValue(actor.Guid, out var existing))
        {
            // the goal here is to deduplicate actors with guid collisions by merging their transforms
            // it makes shading inaccurate and can lead to some weirdness with children, but it's a HUGE performance win
            
            Log.Warning("Actor {Name} with GUID {Guid} already exists. Merging transforms.", actor.Name, actor.Guid);

            // add the new actor's transform as an instance of the existing actor (this will shade both instances the same way, which may be inaccurate)
            actor.Transform.UpdateLocalMatrix();
            existing.InstancedTransform.Transforms.Add(new InstancedTransform(actor.Transform.LocalMatrix));
            var instanceIndex = existing.InstancedTransform.Transforms.Count - 1;

            // transfer instances from the new actor to the existing one (they have the same parent transform, so this is valid)
            foreach (var instancedTransform in actor.InstancedTransform.Transforms)
            {
                existing.InstancedTransform.Transforms.Add(new InstancedTransform(instancedTransform.LocalMatrix));
            }

            // instance all known children so that they render on all the existing actor's instances
            // this is a BIG hack because you can't assume the new actor has the same children as the existing one
            // but we don't know at this stage what the new actor's children are, so this is the best we can do
            foreach (var child in existing.Children)
            {
                child.Transform.UpdateLocalMatrix();
                child.InstancedTransform.Transforms.Add(new InstancedTransform(child.Transform.LocalMatrix, instanceIndex));
            }
            return;
        }
        
        _dict.Add(actor.Guid, actor);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, actor));
    }
    
    public bool Remove(Actor actor)
    {
        var removed = _dict.Remove(actor.Guid);
        if (removed) CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, actor));
        return removed;
    }
    
    public IEnumerator<Actor> GetEnumerator() => _dict.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public ICollection<FGuid> Keys => _dict.Keys;
    public ICollection<Actor> Values => _dict.Values;
    public int Count => _dict.Count;
    public bool IsReadOnly => false;


    IEnumerator<KeyValuePair<FGuid, Actor>> IEnumerable<KeyValuePair<FGuid, Actor>>.GetEnumerator() => throw new NotImplementedException();
    public void Add(FGuid key, Actor value) => throw new NotImplementedException();
    public bool ContainsKey(FGuid key) => throw new NotImplementedException();
    public bool Remove(FGuid key) => throw new NotImplementedException();
    public bool TryGetValue(FGuid key, [MaybeNullWhen(false)] out Actor value) => throw new NotImplementedException();

    public Actor this[FGuid key]
    {
        get => _dict[key];
        set => throw new NotImplementedException();
    }
    
    public void Add(KeyValuePair<FGuid, Actor> item) => throw new NotImplementedException();
    public void Clear() => throw new NotImplementedException();
    public bool Contains(KeyValuePair<FGuid, Actor> item) => throw new NotImplementedException();
    public void CopyTo(KeyValuePair<FGuid, Actor>[] array, int arrayIndex) => throw new NotImplementedException();
    public bool Remove(KeyValuePair<FGuid, Actor> item) => throw new NotImplementedException();
}