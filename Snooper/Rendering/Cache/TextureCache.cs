using CUE4Parse.UE4.Objects.Core.Misc;
using Serilog;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Managers;
using Snooper.Rendering.Components.Descriptors;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Snooper.Rendering.Cache;

/// <summary>
/// Owns every texture that lives on the GPU, and tells a material section when its container is ready to draw with.
/// </summary>
public static class TextureCache
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(TextureCache));

    internal sealed class TextureEntry(Texture texture)
    {
        public readonly Texture Texture = texture;
        public BindlessTexture? Bindless; // null until it is uploaded
        public bool Failed; // could not be decoded or uploaded, a container that needs it never completes
        public int RefCount;
        public LinkedListNode<TextureEntry>? Evictable; // its place in line while nothing references it
        public readonly List<(ContainerEntry Container, string Slot)> Waiting = []; // who gets the handle when it lands
    }

    internal sealed class ContainerEntry(string key, IMaterialDataContainer container)
    {
        public readonly string Key = key;
        public readonly IMaterialDataContainer Container = container;
        public readonly List<TextureEntry> Textures = []; // each one once
        public readonly Dictionary<int, MaterialSection> Sections = [];
        public int Remaining; // textures it still waits for
        public bool Completed;
    }

    private readonly record struct Request(MaterialSection Section, string? Key, IMaterialDataContainer? Container);

    private static readonly ConcurrentQueue<Request> _requests = new();
    private static readonly ConcurrentQueue<(TextureEntry Entry, Exception? Error)> _decoded = new();

    private static readonly Dictionary<FGuid, TextureEntry> _textures = [];
    private static readonly Dictionary<string, ContainerEntry> _containers = [];
    private static readonly Dictionary<int, ContainerEntry> _sections = []; // section id > the container it uses
    private static readonly Queue<MaterialSection> _ready = new(); // sections to tell their container is complete
    private static readonly LinkedList<TextureEntry> _evictable = []; // least recently released first

    public const long TextureBudgetBytes = 2L * 1024 * 1024 * 1024; // resident texture memory before unreferenced textures get evicted
    private static int _pending;
    private static int _resident;

    private const int MinReportedBatch = 8;
    private static int _batchTotal;
    private static int _batchDone;

    public static int LoadedTextureCount => _resident;
    public static int PendingTextureCount => _pending;
    public static int UploadQueueCount => _decoded.Count;
    public static int EvictableTextureCount => _evictable.Count;
    public static bool IsLoading => _pending > 0;
    public static long ResidentBytes { get; private set; }

    public static float LoadingProgress => _batchTotal > 0 ? (float) _batchDone / _batchTotal : 1f;

    internal static IReadOnlyCollection<TextureEntry> Textures => _textures.Values;
    internal static IReadOnlyCollection<ContainerEntry> Containers => _containers.Values;
    internal static int NotifyQueueCount => _ready.Count;

    public static IEnumerable<Texture> GetLoaded()
    {
        foreach (var entry in _textures.Values)
        {
            if (entry.Bindless is not null)
                yield return entry.Texture;
        }
    }

    public static bool TryGetBindless(FGuid guid, [MaybeNullWhen(false)] out BindlessTexture bindless)
    {
        bindless = _textures.TryGetValue(guid, out var entry) ? entry.Bindless : null;
        return bindless is not null;
    }

    public static void Add(MaterialSection section)
    {
        var cacheKey = section.CacheKey;
        var inline = string.IsNullOrEmpty(cacheKey);

        var container = inline ? section.InlineContainer : MaterialCache.Resolve(cacheKey!);
        if (container is null) return;

        _requests.Enqueue(new Request(section, inline ? $"__inline_{section.SectionId}" : cacheKey, container));
    }

    public static void Release(MaterialSection section) => _requests.Enqueue(new Request(section, null, null));

    public static void Update(FrameBudget budget)
    {
        while (_requests.TryDequeue(out var request))
        {
            if (request.Container is null) Drop(request.Section);
            else Take(request.Section, request.Key!, request.Container);
        }

        Notify(budget);
        Upload(budget);
        Evict();

        if (_batchTotal >= MinReportedBatch)
        {
            var completed = _pending == 0 ? $"{_batchTotal:N0} textures uploaded" : null;
            Progress.Report("work.textures", Settings.ImagesIcon, "Uploading textures", _batchDone, _batchTotal, completed);
        }

        if (_pending == 0)
            _batchTotal = _batchDone = 0;
    }

    private static void Take(MaterialSection section, string key, IMaterialDataContainer container)
    {
        if (_sections.TryGetValue(section.SectionId, out var previous))
        {
            if (previous.Key == key)
            {
                if (previous.Completed) _ready.Enqueue(section); // asked again for the same one, an edit reverted for example
                return;
            }

            Drop(section); // the section swapped materials
        }

        if (!_containers.TryGetValue(key, out var entry))
            entry = Link(key, container);

        entry.Sections[section.SectionId] = section;
        _sections[section.SectionId] = entry;
        foreach (var texture in entry.Textures)
            Reference(texture);

        if (entry.Completed) _ready.Enqueue(section);
        else if (entry.Remaining == 0) Complete(entry); // nothing to wait for: no textures, or all of them resident already
    }

    private static void Drop(MaterialSection section)
    {
        if (!_sections.Remove(section.SectionId, out var entry)) return;

        entry.Sections.Remove(section.SectionId);
        foreach (var texture in entry.Textures)
            Dereference(texture);

        if (entry.Sections.Count > 0) return;

        // nobody uses this container anymore, so it is forgotten. This is what keeps eviction simple: a texture without
        // references has no container left pointing at its handle. Bringing the container back later is cheap.
        _containers.Remove(entry.Key);
        foreach (var texture in entry.Textures)
            texture.Waiting.RemoveAll(x => x.Container == entry);
    }

    /// <summary>
    /// Ties a container to its textures: the resident ones give their handle right away, the others are waited for.
    /// </summary>
    private static ContainerEntry Link(string key, IMaterialDataContainer container)
    {
        var entry = new ContainerEntry(key, container);
        _containers.Add(key, entry);

        if (!container.HasTextures) return entry;

        foreach (var (slot, texture) in container.GetTextures())
        {
            var item = GetOrLoad(texture);
            if (!entry.Textures.Contains(item))
                entry.Textures.Add(item);

            if (item.Bindless is { } bindless)
            {
                container.SetBindlessTexture(slot, bindless);
                continue;
            }

            entry.Remaining++;
            if (!item.Failed)
                item.Waiting.Add((entry, slot));
        }

        return entry;
    }

    private static TextureEntry GetOrLoad(Texture texture)
    {
        if (_textures.TryGetValue(texture.Guid, out var entry)) return entry;

        entry = new TextureEntry(texture);
        _textures.Add(texture.Guid, entry);
        _pending++;
        _batchTotal++;

        var loading = entry;
        ThreadManager.Enqueue(() =>
        {
            Exception? error = null;
            try
            {
                loading.Texture.Prepare();
            }
            catch (Exception e)
            {
                error = e;
            }

            _decoded.Enqueue((loading, error));
        });

        return entry;
    }

    private static void Reference(TextureEntry texture)
    {
        texture.RefCount++;
        if (texture.Evictable is not { } node) return;

        _evictable.Remove(node);
        texture.Evictable = null;
    }

    private static void Dereference(TextureEntry texture)
    {
        texture.RefCount = Math.Max(0, texture.RefCount - 1);
        if (texture is { RefCount: 0, Bindless: not null })
            texture.Evictable ??= _evictable.AddLast(texture); // one still loading gets in line when it lands
    }

    private static void Complete(ContainerEntry entry)
    {
        entry.Container.FinalizeGpuData();
        entry.Completed = true;

        foreach (var section in entry.Sections.Values)
            _ready.Enqueue(section);
    }

    private const int MinNotifiedPerFrame = 64;

    /// <summary>
    /// Each section writes its material data to the GPU when told, which is why this is metered and not done on the spot.
    /// A section that left in the meantime has no listener anymore and the call does nothing.
    /// </summary>
    private static void Notify(FrameBudget budget)
    {
        var notified = 0;
        while ((notified < MinNotifiedPerFrame || !budget.Exhausted) && _ready.TryDequeue(out var section))
        {
            section.ContainerReady();
            notified++;
        }
    }

    private static void Upload(FrameBudget budget)
    {
        var uploaded = 0;
        while ((uploaded == 0 || !budget.Exhausted) && _decoded.TryDequeue(out var item))
        {
            var (entry, error) = item;
            if (!_textures.TryGetValue(entry.Texture.Guid, out var current) || current != entry)
                continue; // the cache was cleared while a worker still had it

            uploaded++;
            _pending--;
            _batchDone++;

            if (error is null)
            {
                try
                {
                    entry.Texture.Generate();
                }
                catch (Exception e)
                {
                    error = e;
                }
            }

            if (error is not null || !entry.Texture.IsReadyForBindless)
            {
                if (error is not null) Log.Error(error, "Could not load {Name} ({Guid:l})", entry.Texture.Name, entry.Texture.Guid);
                else Log.Warning("{Name} ({Guid:l}) generated nothing to bind", entry.Texture.Name, entry.Texture.Guid);

                entry.Texture.Dispose();
                entry.Failed = true;
                entry.Waiting.Clear();
                continue;
            }

            var texture = entry.Texture;
            Log.Debug("Uploaded {Format:l} with size {Width}x{Height} and {MipCount} mips ({Guid:l})", texture.FormatName, texture.Width, texture.Height, texture.MipCount, texture.Guid);

            var bindless = new BindlessTexture(texture);
            bindless.Generate();
            bindless.MakeResident();

            entry.Bindless = bindless;
            _resident++;
            ResidentBytes += texture.Allocated;

            foreach (var (container, slot) in entry.Waiting)
            {
                container.Container.SetBindlessTexture(slot, bindless);
                if (--container.Remaining == 0)
                    Complete(container);
            }
            entry.Waiting.Clear();

            if (entry.RefCount == 0)
                entry.Evictable ??= _evictable.AddLast(entry); // every section that wanted it left while it was loading
        }
    }

    private static void Evict()
    {
        while (ResidentBytes > TextureBudgetBytes && _evictable.First is { } node)
        {
            var entry = node.Value;
            var texture = entry.Texture;

            _evictable.RemoveFirst();
            entry.Evictable = null;
            _textures.Remove(texture.Guid);

            _resident--;
            ResidentBytes -= texture.Allocated;
            Log.Debug("Evicted {Name} {Width}x{Height} ({Guid:l})", texture.Name, texture.Width, texture.Height, texture.Guid);

            entry.Bindless!.Dispose();
        }
    }

    /// <summary>
    /// Drops every section at once, for a scene transition. The textures stay resident without a reference: the next
    /// scene takes back what it shares, the rest goes when the budget asks for it. Render thread.
    /// </summary>
    public static void Clear()
    {
        _requests.Clear();
        _ready.Clear();
        _sections.Clear();
        _containers.Clear();

        foreach (var texture in _textures.Values)
        {
            texture.RefCount = 0;
            texture.Waiting.Clear();
            if (texture.Bindless is not null)
                texture.Evictable ??= _evictable.AddLast(texture);
        }
    }

    public static void ClearAndDispose()
    {
        Log.Information("Clearing texture cache with {Count} entries", _resident);

        foreach (var texture in _textures.Values)
        {
            if (texture.Bindless is { } bindless) bindless.Dispose();
            else texture.Texture.Dispose();
        }

        _requests.Clear();
        _decoded.Clear();
        _textures.Clear();
        _containers.Clear();
        _sections.Clear();
        _ready.Clear();
        _evictable.Clear();

        _batchTotal = 0;
        _batchDone = 0;
        _pending = 0;
        _resident = 0;
        ResidentBytes = 0;
    }

    public static long Allocated => ResidentBytes;
    public static long Used => ResidentBytes;

    public static IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var entry in _textures.Values)
        {
            if (entry.Bindless is not null)
                yield return new MemoryDetail(entry.Texture.Name, entry.Evictable is null ? "Resident" : "Evictable", entry.Texture);
        }
    }
}
