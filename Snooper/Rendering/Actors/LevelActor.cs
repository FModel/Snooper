using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LevelActor : UnrealActor
{
    private FPackageIndex?[]? _textureData;
    private FSoftObjectPath[]? _additionalWorlds;

    public LevelActor(UObject actor) : base(actor)
    {
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?>("RootComponent"));

        if (actor is AInstancedFoliageActor { FoliageInfos: { } foliages })
        {
            foreach (var foliage in foliages.Values)
            {
                switch (foliage.Implementation)
                {
                    case FFoliageStaticMesh staticMesh:
                    {
                        EnqueuePointers(staticMesh.Component);
                        break;
                    }
                    // case FFoliageActor:
                    // {
                    //     throw new NotImplementedException("FoliageActor is not supported yet");
                    // }
                }
            }
        }

        actor.TryGetAllValues(out _textureData, "TextureData");
        actor.TryGetValue(out _additionalWorlds, "AdditionalWorlds");
    }

    public void ProcessEnqueuedComponents(Dictionary<ResolvedObject, SpatialComponent> components, Dictionary<ResolvedObject, LevelActor> actors)
    {
        foreach (var ptr in _ptrs)
        {
            CreateComponentRecursive(components, actors, ptr);
        }
        _textureData = null;
        _ptrs.Clear();

        foreach (var additionalWorld in _additionalWorlds ?? [])
        {
            if (!additionalWorld.TryLoad<UWorld>(out var w)) continue;
            Children.Add(new CellActor(additionalWorld, w, true)); // after the root, which it hangs off
        }
        _additionalWorlds = null;
    }

    private void CreateComponentRecursive(Dictionary<ResolvedObject, SpatialComponent> components, Dictionary<ResolvedObject, LevelActor> actors, FPackageIndex ptr)
    {
        if (ptr.ResolvedObject is not { } resolved || components.ContainsKey(resolved)) return;

        if (ptr.Load()?.Outer is { } outer && actors.TryGetValue(outer, out var owner) && owner != this)
        {
            // the actor that owns this component already exists, and is not this actor, so we need to create the component under that actor instead
            owner.CreateComponentRecursive(components, actors, ptr);
            return;
        }

        var pair = CreateComponentPair(ptr);
        if (_textureData?.Length > 0 && pair.Component is StaticMeshComponent staticMeshComponent)
        {
            for (var i = 0; i < _textureData.Length; i++)
            {
                var dataPtr = _textureData[i];
                if (dataPtr == null || dataPtr.IsNull || !dataPtr.TryLoad<UBuildingTextureData>(out var textureData))
                    continue;

                staticMeshComponent.RegisterTextureData(textureData, i);
            }
        }

        if (pair.ParentPtr is { ResolvedObject: { } parent })
        {
            CreateComponentRecursive(components, actors, pair.ParentPtr);
            pair.Component.Relation = components[parent];
        }

        if (pair is { LeaderPtr: { ResolvedObject: { } leader } leaderPtr, Component: SkinnedMeshComponent skinned })
        {
            CreateComponentRecursive(components, actors, leaderPtr);
            skinned.Leader = components[leader] as SkinnedMeshComponent;
        }

        components.TryAdd(resolved, pair.Component);
        Components.Add(pair.Component);
    }

    private readonly HashSet<FPackageIndex> _ptrs = [];
    internal void EnqueuePointers(params FPackageIndex?[] ptrs)
    {
        foreach (var ptr in ptrs)
        {
            if (ptr is { IsNull: false })
            {
                _ptrs.Add(ptr);
            }
        }
    }
}
