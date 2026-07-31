using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core;

/// <summary>Rolling per-frame history of one timing, with running max/average over the window.</summary>
public class ProfilerMetricData
{
    public const int MaxFrameHistory = 100;

    public float[] TimeElapsedMs { get; } = new float[MaxFrameHistory];
    public float MaxTimeElapsedMs { get; private set; }
    public float AverageTimeElapsedMs { get; private set; }
    public float LastTimeElapsedMs => TimeElapsedMs[0];
    public float AllTimeMaxTimeElapsedMs { get; private set; }

    public void AddTimeSample(float ms)
    {
        var max = ms;
        var sum = ms;
        for (var i = TimeElapsedMs.Length - 1; i > 0; i--)
        {
            var shifted = TimeElapsedMs[i] = TimeElapsedMs[i - 1];
            if (shifted > max) max = shifted;
            sum += shifted;
        }

        TimeElapsedMs[0] = ms;
        MaxTimeElapsedMs = max;
        AverageTimeElapsedMs = sum / TimeElapsedMs.Length;

        if (ms > AllTimeMaxTimeElapsedMs)
            AllTimeMaxTimeElapsedMs = ms;
    }

    public void Reset()
    {
        Array.Clear(TimeElapsedMs);
        MaxTimeElapsedMs = 0;
        AverageTimeElapsedMs = 0;
    }
}

/// <summary>One zone in the profiler tree. Timings accumulate during a frame and are committed to the rolling histories
/// by <see cref="Flush"/>; primitive counts roll up from the children at the same time.</summary>
public sealed class ProfilerNode(string name)
{
    public string Name { get; } = name;
    public ProfilerMetricData Cpu { get; } = new();
    public ProfilerMetricData Gpu { get; } = new();

    /// <summary>True once any GPU time has been recorded for this zone.</summary>
    public bool HasGpu { get; private set; }

    /// <summary>
    /// Primitives generated last frame by this zone and everything under it. Only leaf draw zones hold a query, so a
    /// pass or a system must be read through this rather than its own count, which would always be zero.
    /// </summary>
    public long TotalPrimitives { get; private set; }

    /// <summary>True when this zone or anything under it counts primitives.</summary>
    public bool HasPrimitives { get; private set; }

    private readonly Dictionary<string, ProfilerNode> _childLookup = [];
    private readonly List<ProfilerNode> _children = [];
    public IReadOnlyList<ProfilerNode> Children => _children;

    private double _cpuAccum;
    private double _gpuAccum;
    private long _primAccum;
    private bool _countsPrimitives;

    internal ProfilerNode GetOrAddChild(string name)
    {
        if (!_childLookup.TryGetValue(name, out var child))
        {
            child = new ProfilerNode(name);
            _childLookup.Add(name, child);
            _children.Add(child);
        }
        return child;
    }

    internal void AddCpu(double ms) => _cpuAccum += ms;

    internal void AddGpu(double ms)
    {
        _gpuAccum += ms;
        HasGpu = true;
    }

    internal void AddPrimitives(long count)
    {
        _primAccum += count;
        _countsPrimitives = true;
    }

    internal void Flush()
    {
        Cpu.AddTimeSample((float)_cpuAccum);
        Gpu.AddTimeSample((float)_gpuAccum);

        TotalPrimitives = _primAccum;
        HasPrimitives = _countsPrimitives;
        _cpuAccum = 0;
        _gpuAccum = 0;
        _primAccum = 0;

        foreach (var child in _children)
        {
            child.Flush();
            TotalPrimitives += child.TotalPrimitives;
            HasPrimitives |= child.HasPrimitives;
        }
    }
}

/// <summary>Handle for an open zone; closing it (via <c>using</c>) records the elapsed CPU/GPU and any query result.</summary>
public readonly struct ProfilerScope(ProfilerNode? node, long cpuStart, int gpuBegin, int gpuEnd, int primQuery) : IDisposable
{
    internal readonly ProfilerNode? _node = node;
    internal readonly long _cpuStart = cpuStart;
    internal readonly int _gpuBegin = gpuBegin;
    internal readonly int _gpuEnd = gpuEnd;
    internal readonly int _primQuery = primQuery;

    public void Dispose() => Profiler.End(this);
}

/// <summary>
/// Frame-scoped GPU/CPU profiler. Everything here — opening zones, flushing, and the UI reading the tree — runs on the
/// single GL/main thread; it is deliberately not thread-safe. Add synchronization before profiling any worker thread.
/// </summary>
public static class Profiler
{
    /// <summary>
    /// UI-facing toggle, applied at the next <see cref="BeginFrame"/>. The toolbar that flips it is drawn from inside
    /// the frame being profiled, so acting on it immediately would let zones open after the frame zone was skipped —
    /// with an empty stack those hang themselves off the root as bogus top-level zones that never go away.
    /// </summary>
    public static ref bool Enabled => ref _requested;

    private static bool _requested;

    /// <summary>Latched at the frame boundary; the whole frame is profiled or none of it is.</summary>
    private static bool _active;

    [Flags]
    private enum Track
    {
        None = 0,
        Cpu = 1,
        Gpu = 2,
        Primitives = 4, // implies Gpu; counts primitives the zone's draws generate
    }

    private const string FrameZone = "Frame";

    private static readonly Stack<ProfilerNode> _stack = new();

    // Query id pools, kept apart because a GL query object takes its type from first use: a timestamp query cannot be
    // recycled as a PRIMITIVES_GENERATED one.
    private static readonly QueryPool _timestampQueries = new();
    private static readonly QueryPool _primitiveQueries = new();
    private static readonly Queue<PendingTime> _pendingTime = [];
    private static readonly Queue<PendingCount> _pendingCount = [];
    private static int _activePrimQuery = -1;
    private static ProfilerScope _frameScope;

    /// <summary>Root of the zone tree; its children are the frame's top-level zones. Only read after <see cref="EndFrame"/>.</summary>
    public static ProfilerNode Root { get; } = new("Root");

    /// <summary>The "Frame" zone. Always present, so callers never have to index into <see cref="Root"/> by position.</summary>
    public static ProfilerNode Frame { get; } = Root.GetOrAddChild(FrameZone);

    /// <summary>Primitives generated across every draw of the last frame. Holds its last value once disabled.</summary>
    public static long TotalPrimitives { get; private set; }

    /// <summary>Times CPU only.</summary>
    public static ProfilerScope Cpu(string name) => Begin(name, Track.Cpu);

    /// <summary>Times GPU only.</summary>
    public static ProfilerScope Gpu(string name) => Begin(name, Track.Gpu);

    /// <summary>Times CPU and GPU.</summary>
    public static ProfilerScope Sample(string name) => Begin(name, Track.Cpu | Track.Gpu);

    /// <summary>
    /// A "Draw" zone: GPU time plus the primitives its draws generate, post-tessellation. These must not nest —
    /// PRIMITIVES_GENERATED is a scoped query and GL allows only one active per target at a time.
    /// </summary>
    public static ProfilerScope Draw() => Begin(nameof(Draw), Track.Gpu | Track.Primitives);

    /// <summary>A "Cull" zone: GPU time of a culling dispatch.</summary>
    public static ProfilerScope Cull() => Begin(nameof(Cull), Track.Gpu);

    /// <summary>Matures last frame's GPU results and opens the root "Frame" zone. Pair with <see cref="EndFrame"/>; GL thread only.</summary>
    public static void BeginFrame()
    {
        _active = _requested;
        if (!_active) return;

        MatureQueries();
        _frameScope = Begin(FrameZone, Track.Cpu | Track.Gpu);
    }

    /// <summary>Closes the frame zone and commits every accumulator into its rolling history.</summary>
    public static void EndFrame()
    {
        End(_frameScope);
        _frameScope = default;
        if (!_active) return;

        Root.Flush();
        TotalPrimitives = Root.TotalPrimitives;
    }

    private static ProfilerScope Begin(string name, Track track)
    {
        if (!_active) return default;

        var node = (_stack.Count > 0 ? _stack.Peek() : Root).GetOrAddChild(name);
        _stack.Push(node);

        var cpuStart = track.HasFlag(Track.Cpu) ? Stopwatch.GetTimestamp() : 0L;

        var gpuBegin = -1;
        var gpuEnd = -1;
        if (track.HasFlag(Track.Gpu))
        {
            gpuBegin = _timestampQueries.Rent();
            gpuEnd = _timestampQueries.Rent();
            GL.QueryCounter(gpuBegin, QueryCounterTarget.Timestamp);
        }

        var primQuery = -1;
        if (track.HasFlag(Track.Primitives))
        {
            // Only one scoped query may be active. A nested Draw zone is skipped so the outer keeps counting; the assert
            // surfaces the mistake in development.
            Debug.Assert(_activePrimQuery < 0, $"Draw zone '{name}' nested inside another; its primitives go uncounted.");
            if (_activePrimQuery < 0)
            {
                primQuery = _primitiveQueries.Rent();
                _activePrimQuery = primQuery;
                GL.BeginQuery(QueryTarget.PrimitivesGenerated, primQuery);
            }
        }

        return new ProfilerScope(node, cpuStart, gpuBegin, gpuEnd, primQuery);
    }

    internal static void End(ProfilerScope scope)
    {
        if (scope._node == null) return;

        if (_stack.Count > 0) _stack.Pop();

        if (scope._cpuStart != 0)
        {
            var ms = (Stopwatch.GetTimestamp() - scope._cpuStart) * 1000.0 / Stopwatch.Frequency;
            scope._node.AddCpu(ms);
        }

        if (scope._gpuBegin >= 0)
        {
            GL.QueryCounter(scope._gpuEnd, QueryCounterTarget.Timestamp);
            _pendingTime.Enqueue(new PendingTime(scope._node, scope._gpuBegin, scope._gpuEnd));
        }

        if (scope._primQuery >= 0)
        {
            GL.EndQuery(QueryTarget.PrimitivesGenerated);
            _activePrimQuery = -1;
            _pendingCount.Enqueue(new PendingCount(scope._node, scope._primQuery));
        }
    }

    /// <summary>
    /// Reads back the GPU results that have matured, oldest first. Queries complete in submission order, so each loop
    /// stops at the first one not yet available rather than stalling the CPU on the GPU.
    /// </summary>
    private static void MatureQueries()
    {
        while (_pendingTime.Count > 0)
        {
            var p = _pendingTime.Peek();
            GL.GetQueryObject(p.End, GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available == 0) break;

            GL.GetQueryObject(p.Begin, GetQueryObjectParam.QueryResult, out long begin);
            GL.GetQueryObject(p.End, GetQueryObjectParam.QueryResult, out long end);
            p.Node.AddGpu((end - begin) / 1_000_000.0);

            _timestampQueries.Return(p.Begin);
            _timestampQueries.Return(p.End);
            _pendingTime.Dequeue();
        }

        while (_pendingCount.Count > 0)
        {
            var p = _pendingCount.Peek();
            GL.GetQueryObject(p.Query, GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available == 0) break;

            GL.GetQueryObject(p.Query, GetQueryObjectParam.QueryResult, out long count);
            p.Node.AddPrimitives(count);

            _primitiveQueries.Return(p.Query);
            _pendingCount.Dequeue();
        }
    }

    /// <summary>Recycles GL query ids so the steady state issues no new query allocations.</summary>
    private sealed class QueryPool
    {
        private readonly Queue<int> _free = [];
        public int Rent() => _free.Count > 0 ? _free.Dequeue() : GL.GenQuery();
        public void Return(int query) => _free.Enqueue(query);
    }

    private readonly record struct PendingTime(ProfilerNode Node, int Begin, int End);
    private readonly record struct PendingCount(ProfilerNode Node, int Query);
}
