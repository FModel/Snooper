using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core;

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
        for (var i = TimeElapsedMs.Length - 1; i > 0; i--)
            TimeElapsedMs[i] = TimeElapsedMs[i - 1];

        TimeElapsedMs[0] = ms;
        MaxTimeElapsedMs = TimeElapsedMs.Max();
        AverageTimeElapsedMs = TimeElapsedMs.Average();

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

public sealed class ProfilerNode
{
    public string Name { get; }
    public ProfilerMetricData Cpu { get; } = new();
    public ProfilerMetricData Gpu { get; } = new();

    /// <summary>True once at least one GPU sample has been recorded for this zone.</summary>
    public bool HasGpu { get; private set; }

    private readonly Dictionary<string, ProfilerNode> _childLookup = [];
    private readonly List<ProfilerNode> _children = [];
    public IReadOnlyList<ProfilerNode> Children => _children;

    private double _cpuAccum;
    private double _gpuAccum;

    internal ProfilerNode(string name)
    {
        Name = name;
    }

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

    internal void Flush()
    {
        Cpu.AddTimeSample((float)_cpuAccum);
        Gpu.AddTimeSample((float)_gpuAccum);
        _cpuAccum = 0;
        _gpuAccum = 0;

        foreach (var child in _children)
            child.Flush();
    }
}

public readonly struct ProfilerScope : IDisposable
{
    internal readonly ProfilerNode? _node;
    internal readonly long _cpuStart;
    internal readonly int _gpuBegin;
    internal readonly int _gpuEnd;

    internal ProfilerScope(ProfilerNode? node, long cpuStart, int gpuBegin, int gpuEnd)
    {
        _node = node;
        _cpuStart = cpuStart;
        _gpuBegin = gpuBegin;
        _gpuEnd = gpuEnd;
    }

    public void Dispose() => Profiler.End(this);
}

public static class Profiler
{
    public static bool Enabled = false;

    private static readonly Lock _treeLock = new();
    private static readonly ProfilerNode _rootNode = new("Root");
    private static readonly ThreadLocal<Stack<ProfilerNode>> _stacks = new(() => new Stack<ProfilerNode>());

    // GL-thread-only state (query pool + in-flight timestamp pairs).
    private static readonly Queue<int> _freeQueries = [];
    private static readonly Queue<PendingGpu> _pending = [];

    private static int? _glThreadId;
    private static ProfilerScope _frameScope;

    /// <summary>
    /// Reads the profiler tree under the tree lock. Use this (rather than touching
    /// <see cref="ProfilerNode.Children"/> directly) whenever a reader may run while zones
    /// are being opened on other threads, e.g. the editor UI walking the tree while a
    /// worker thread times work. The callback receives the tree root.
    /// </summary>
    public static void Read(Action<ProfilerNode> reader)
    {
        lock (_treeLock)
        {
            reader(_rootNode);
        }
    }

    private static bool IsGlThread => _glThreadId == Environment.CurrentManagedThreadId;

    /// <summary>Times CPU only. Safe to call from any thread.</summary>
    public static ProfilerScope Cpu(string name) => Begin(name, true, false);

    /// <summary>Times GPU only (GL thread only; no-op on other threads).</summary>
    public static ProfilerScope Gpu(string name) => Begin(name, false, true);

    /// <summary>Times both CPU and GPU. GPU part is only measured on the GL thread.</summary>
    public static ProfilerScope Sample(string name) => Begin(name, true, true);

    /// <summary>Begins a frame: matures pending GPU results and opens the root "Frame" zone.
    /// Must be paired with <see cref="EndFrame"/> and called from the GL thread.</summary>
    public static void BeginFrame()
    {
        _glThreadId ??= Environment.CurrentManagedThreadId;

        if (!Enabled) return;

        ReadbackGpu();
        _frameScope = Begin("Frame", true, true);
    }

    /// <summary>Ends the frame opened by <see cref="BeginFrame"/> and flushes all accumulated
    /// samples into their rolling histories.</summary>
    public static void EndFrame()
    {
        // Always close the frame scope (keeps the stack balanced even if profiling was
        // toggled off mid-frame), but skip the flush entirely when disabled so a disabled
        // profiler does no per-frame work.
        End(_frameScope);
        _frameScope = default;

        if (!Enabled) return;

        lock (_treeLock)
        {
            _rootNode.Flush();
        }
    }

    private static ProfilerScope Begin(string name, bool cpu, bool gpu)
    {
        if (!Enabled) return default;

        var stack = _stacks.Value!;

        ProfilerNode node;
        lock (_treeLock)
        {
            var parent = stack.Count > 0 ? stack.Peek() : _rootNode;
            node = parent.GetOrAddChild(name);
        }
        stack.Push(node);

        var cpuStart = cpu ? Stopwatch.GetTimestamp() : 0L;

        var gpuBegin = -1;
        var gpuEnd = -1;
        if (gpu && IsGlThread)
        {
            gpuBegin = RentQuery();
            gpuEnd = RentQuery();
            GL.QueryCounter(gpuBegin, QueryCounterTarget.Timestamp);
        }

        return new ProfilerScope(node, cpuStart, gpuBegin, gpuEnd);
    }

    internal static void End(ProfilerScope scope)
    {
        if (scope._node == null) return;

        var stack = _stacks.Value!;
        if (stack.Count > 0) stack.Pop();

        if (scope._cpuStart != 0)
        {
            var ms = (Stopwatch.GetTimestamp() - scope._cpuStart) * 1000.0 / Stopwatch.Frequency;
            lock (_treeLock)
            {
                scope._node.AddCpu(ms);
            }
        }

        if (scope._gpuBegin >= 0)
        {
            GL.QueryCounter(scope._gpuEnd, QueryCounterTarget.Timestamp);
            _pending.Enqueue(new PendingGpu(scope._node, scope._gpuBegin, scope._gpuEnd));
        }
    }

    private static void ReadbackGpu()
    {
        // Timestamp queries complete in submission order, so the front of the queue
        // matures first. Stop as soon as the oldest is not yet available: no busy-wait.
        while (_pending.Count > 0)
        {
            var p = _pending.Peek();

            GL.GetQueryObject(p.End, GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available == 0) break;

            GL.GetQueryObject(p.Begin, GetQueryObjectParam.QueryResult, out long begin);
            GL.GetQueryObject(p.End, GetQueryObjectParam.QueryResult, out long end);

            p.Node.AddGpu((end - begin) / 1_000_000.0);

            _freeQueries.Enqueue(p.Begin);
            _freeQueries.Enqueue(p.End);
            _pending.Dequeue();
        }
    }

    private static int RentQuery() => _freeQueries.Count > 0 ? _freeQueries.Dequeue() : GL.GenQuery();

    private readonly record struct PendingGpu(ProfilerNode Node, int Begin, int End);
}
