using CUE4Parse.GameTypes.FN.Assets.Exports.DataAssets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.Rendering.Actors;

public class LevelActor : UnrealActor
{
    private readonly FPackageIndex?[] _textureData;

    public LevelActor(UObject actor, Dictionary<FPackageIndex, SpatialComponent> components) : base(actor)
    {
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?>("RootComponent"));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("InstanceComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("BlueprintCreatedComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?[]>("LandscapeComponents", []));
        EnqueuePointers(actor.GetOrDefault<FPackageIndex?>("SplineComponent"));

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
                    case FFoliageActor:
                    {
                        throw new NotImplementedException("FoliageActor is not supported yet");
                    }
                }
            }
        }

        actor.TryGetAllValues(out _textureData, "TextureData");

        // ProcessRootComponent
        foreach (var ptr in _ptrs)
        {
            _parent = CreateComponentRecursive(components, ptr).ParentPtr;
            _ptrs.Remove(ptr);
            break;
        }

        if (actor.TryGetValue(out FSoftObjectPath[] additionalWorlds, "AdditionalWorlds"))
        {
            foreach (var additionalWorld in additionalWorlds)
            {
                if (!additionalWorld.TryLoad<UWorld>(out var w)) continue;
                Children.Add(new WorldActor(w));
            }
        }
    }

    public FPackageIndex? ProcessEnqueuedComponents(Dictionary<FPackageIndex, SpatialComponent> components)
    {
        foreach (var ptr in _ptrs)
        {
            CreateComponentRecursive(components, ptr);
        }

        _ptrs.Clear();
        return _parent;
    }

    private ComponentPair CreateComponentRecursive(Dictionary<FPackageIndex, SpatialComponent> components, FPackageIndex ptr)
    {
        var pair = CreateComponentPair(ptr);
        if (pair.Component is StaticMeshComponent staticMeshComponent)
        {
            for (var i = 0; i < _textureData.Length; i++)
            {
                var dataPtr = _textureData[i];
                if (dataPtr == null || dataPtr.IsNull || !dataPtr.TryLoad<UBuildingTextureData>(out var textureData))
                    continue;

                staticMeshComponent.RegisterTextureData(textureData, i);
            }
        }

        ComponentPair? root = null;
        if (pair.ParentPtr is { IsNull: false })
        {
            if (!components.ContainsKey(pair.ParentPtr))
                root = CreateComponentRecursive(components, pair.ParentPtr);

            pair.Component.Relation = components[pair.ParentPtr];
        }

        components.TryAdd(ptr, pair.Component);
        Components.Add(pair.Component);
        return root ?? pair;
    }

    private readonly FPackageIndex? _parent;
    private readonly HashSet<FPackageIndex> _ptrs = [];
    private void EnqueuePointers(params FPackageIndex?[] ptrs)
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
