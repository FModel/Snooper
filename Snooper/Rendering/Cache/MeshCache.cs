using System.Collections.Concurrent;
using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Cache;

public static class MeshCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(MeshCache));

    private static readonly ConcurrentDictionary<(FGuid, Type), object> _cache = new();

    public static PrimitiveDescriptor<TVertex> GetOrCreate<TVertex>(FGuid guid, Func<PrimitiveDescriptor<TVertex>> factory) where TVertex : unmanaged
    {
        var key = (guid, typeof(TVertex));

        if (_cache.TryGetValue(key, out var cached))
        {
            Log.Verbose("Cache hit for descriptor {Guid} with vertex type {VertexType}", guid, typeof(TVertex).Name);
            return (PrimitiveDescriptor<TVertex>)cached;
        }

        Log.Debug("Cache miss for descriptor {Guid} with vertex type {VertexType}, creating new descriptor", guid, typeof(TVertex).Name);
        var descriptor = factory();
        _cache[key] = descriptor;
        return descriptor;
    }

    public static void ClearAndDispose()
    {
        foreach (var cached in _cache.Values)
        {
            if (cached is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        Log.Information("Clearing primitive descriptor cache with {Count} entries", _cache.Count);
        _cache.Clear();
    }
}
