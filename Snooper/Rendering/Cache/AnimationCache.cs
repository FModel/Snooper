using System.Collections.Concurrent;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Extensions;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Descriptors.Animations;

namespace Snooper.Rendering.Cache;

public static class AnimationCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(AnimationCache));

    private static readonly ConcurrentDictionary<FGuid, Lazy<SkeletonDescriptor>> _skeletons = new();
    private static readonly ConcurrentDictionary<FGuid, Lazy<SequenceBaseDescriptor>> _animations = new();

    public static IEnumerable<SequenceBaseDescriptor> Animations => _animations.Values.Where(x => x.IsValueCreated).Select(x => x.Value);
    public static IEnumerable<SkeletonDescriptor> Skeletons => _skeletons.Values.Where(x => x.IsValueCreated).Select(x => x.Value);

    public static SkeletonDescriptor GetOrCreate(USkeleton skeleton) => GetOrCreate(_skeletons, skeleton.Guid, () =>
    {
        var descriptor = new SkeletonDescriptor(skeleton.ReferenceSkeleton);
        descriptor.SetOwner(skeleton);
        return descriptor;
    });

    public static SequenceDescriptor GetOrCreate(UAnimSequence sequence) => (SequenceDescriptor) GetOrCreate(_animations, sequence.ToGuid(), () => new SequenceDescriptor(sequence));
    public static SequenceBaseDescriptor? GetOrCreate(UAnimationAsset animation) => animation switch
    {
        UAnimMontage montage => GetOrCreate(_animations, montage.ToGuid(), () => new MontageDescriptor(montage)),
        UAnimComposite composite => GetOrCreate(_animations, composite.ToGuid(), () => new CompositeDescriptor(composite)),
        UAnimSequence sequence => GetOrCreate(sequence),
        _ => null
    };

    private static T GetOrCreate<T>(ConcurrentDictionary<FGuid, Lazy<T>> cache, FGuid guid, Func<T> factory)
    {
        var lazy = cache.GetOrAdd(guid, _ => new Lazy<T>(() =>
        {
            Log.Debug("Cache miss for {Type} {Guid}, creating new descriptor", typeof(T).Name, guid);
            return factory();
        }, LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return lazy.Value;
        }
        catch
        {
            cache.TryRemove(guid, out _);
            throw;
        }
    }

    public static void Clear()
    {
        Log.Information("Clearing animation cache with {Animations} animations and {Skeletons} skeletons", _animations.Count, _skeletons.Count);
        _animations.Clear();
        _skeletons.Clear();
    }
}
