using OpenTK.Graphics.OpenGL4;

namespace Snooper.Core;

public class SystemProfiler : IDisposable
{
    public const int MaxFrameHistory = 100;
    
    public long PrimitivesGenerated { get; private set; }
    
    public float[] TimeElapsedMs { get; } = new float[MaxFrameHistory];
    public float MaxTimeElapsedMs { get; private set; }
    public float AverageTimeElapsedMs { get; private set; }
    
    private readonly Dictionary<QueryTarget, int> _activeQueries = new();
    private readonly Dictionary<QueryTarget, int> _pendingQueries = new();
    
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
        foreach (var target in _activeQueries.Keys)
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
        foreach (var (target, query) in _pendingQueries)
        {
            var available = 0;
            while (available == 0)
                GL.GetQueryObject(query, GetQueryObjectParam.QueryResultAvailable, out available);
        
            GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long result);
            GL.DeleteQuery(query);
            _pendingQueries.Remove(target);
        
            switch (target)
            {
                case QueryTarget.TimeElapsed:
                {
                    AddTimeSample(result / 1_000_000f);
                    break;
                }
                case QueryTarget.PrimitivesGenerated:
                {
                    PrimitivesGenerated = result;
                    break;
                }
            }
        }
    }
    
    private void AddTimeSample(float ms)
    {
        for (var i = TimeElapsedMs.Length - 1; i > 0; i--)
            TimeElapsedMs[i] = TimeElapsedMs[i - 1];

        TimeElapsedMs[0] = ms;
        MaxTimeElapsedMs = TimeElapsedMs.Max();
        AverageTimeElapsedMs = TimeElapsedMs.Average();
    }

    public void Dispose()
    {
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