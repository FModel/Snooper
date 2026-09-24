using System.Collections.Concurrent;
using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Cache;

public static class MeshCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(MeshCache));

    private static readonly ConcurrentDictionary<(FGuid, Type), Lazy<object>> _cache = new();

    public static PrimitiveDescriptor<TVertex> GetOrCreate<TVertex>(FGuid guid, Func<PrimitiveDescriptor<TVertex>> factory) where TVertex : unmanaged
    {
        var key = (guid, typeof(TVertex));

        var lazy = _cache.GetOrAdd(key, _ => new Lazy<object>(() =>
        {
            Log.Debug("Cache miss for descriptor {Guid} with vertex type {VertexType}, creating new descriptor", guid, typeof(TVertex).Name);
            return factory();
        }, LazyThreadSafetyMode.ExecutionAndPublication));

        if (lazy.IsValueCreated)
            Log.Verbose("Cache hit for descriptor {Guid} with vertex type {VertexType}", guid, typeof(TVertex).Name);

        try
        {
            return (PrimitiveDescriptor<TVertex>)lazy.Value;
        }
        catch
        {
            _cache.TryRemove(key, out _);
            throw;
        }
    }

    public static void ClearAndDispose()
    {
        foreach (var cached in _cache.Values)
        {
            if (cached is { IsValueCreated: true, Value: IDisposable disposable })
                disposable.Dispose();
        }

        Log.Information("Clearing primitive descriptor cache with {Count} entries", _cache.Count);
        _cache.Clear();
    }
}
