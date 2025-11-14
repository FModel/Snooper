using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core;

public enum ProfilerMetric
{
    Render,
    Update,
    Load,
    Custom
}

public class ProfilerMetricData
{
    public const int MaxFrameHistory = 100;
    
    public float[] TimeElapsedMs { get; } = new float[MaxFrameHistory];
    public float MaxTimeElapsedMs { get; private set; }
    public float AverageTimeElapsedMs { get; private set; }
    public float LastTimeElapsedMs => TimeElapsedMs[0];
    public float AllTimeMaxTimeElapsedMs { get; private set; }
    
    private readonly Stopwatch _stopwatch = new();
    
    public void Begin()
    {
        _stopwatch.Restart();
    }
    
    public void End()
    {
        _stopwatch.Stop();
        AddTimeSample((float)_stopwatch.Elapsed.TotalMilliseconds);
    }
    
    private void AddTimeSample(float ms)
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
        _stopwatch.Reset();
    }
}

public class SystemProfiler : IDisposable
{
    public const int MaxFrameHistory = ProfilerMetricData.MaxFrameHistory;
    
    public long PrimitivesGenerated { get; private set; } // OpenGL

    private readonly Dictionary<ProfilerMetric, ProfilerMetricData> _metrics = new()
    {
        { ProfilerMetric.Render, new ProfilerMetricData() },
        { ProfilerMetric.Update, new ProfilerMetricData() },
        { ProfilerMetric.Load, new ProfilerMetricData() },
    };
    private readonly Dictionary<QueryTarget, int> _activeQueries = new();
    private readonly Dictionary<QueryTarget, int> _pendingQueries = new();
    
    public IReadOnlyDictionary<ProfilerMetric, ProfilerMetricData> GetAllMetrics() => _metrics;

    public ProfilerMetricData GetMetric(ProfilerMetric metric)
    {
        if (!_metrics.TryGetValue(metric, out var data))
        {
            data = new ProfilerMetricData();
            _metrics[metric] = data;
        }
        return data;
    }
    
    public void BeginTiming(ProfilerMetric metric) => GetMetric(metric).Begin();
    public void EndTiming(ProfilerMetric metric) => GetMetric(metric).End();
    
    public void Time(ProfilerMetric metric, Action action)
    {
        BeginTiming(metric);
        try
        {
            action();
        }
        finally
        {
            EndTiming(metric);
        }
    }
    
    public void BeginQuery(params QueryTarget[] targets)
    {
        foreach (var target in targets)
        {
            if (_activeQueries.ContainsKey(target))
                throw new InvalidOperationException($"A query for target {target} is already active. End the previous query before starting a new one.");
        
            if (_pendingQueries.TryGetValue(target, out var old))
            {
                GL.DeleteQuery(old);
                _pendingQueries.Remove(target);
            }
            
            var query = GL.GenQuery();
            _activeQueries.Add(target, query);
            GL.BeginQuery(target, query);
        }
    }

    public void EndQuery()
    {
        foreach (var target in _activeQueries.Keys.ToArray())
        {
            EndQuery(target);
        }
    }
    
    public void EndQuery(QueryTarget target)
    {
        if (!_activeQueries.Remove(target, out var query))
            throw new InvalidOperationException($"No query for target {target} is currently active. Call BeginQuery before ending a query.");

        GL.EndQuery(target);
        _pendingQueries.Add(target, query);
    }

    public void PollResults()
    {
        foreach (var (target, query) in _pendingQueries.ToArray())
        {
            var available = 0;
            while (available == 0)
                GL.GetQueryObject(query, GetQueryObjectParam.QueryResultAvailable, out available);
        
            GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long result);
            GL.DeleteQuery(query);
            _pendingQueries.Remove(target);
        
            switch (target)
            {
                case QueryTarget.PrimitivesGenerated:
                {
                    PrimitivesGenerated = result;
                    break;
                }
            }
        }
    }
    
    public void Reset()
    {
        foreach (var metric in _metrics.Values)
        {
            metric.Reset();
        }
        PrimitivesGenerated = 0;
    }

    public void Dispose()
    {
        Reset();
        _metrics.Clear();
        
        foreach (var query in _activeQueries.Values)
        {
            GL.DeleteQuery(query);
        }
        
        foreach (var query in _pendingQueries.Values)
        {
            GL.DeleteQuery(query);
        }
        
        _activeQueries.Clear();
        _pendingQueries.Clear();
    }
}