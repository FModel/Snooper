using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;

namespace Snooper.Core.Managers;

public class ThreadManager : IDisposable
{
    private readonly Worker[] _workers;
    private long _totalJobsEnqueued;
    private int _nextWorkerIndex;

    public int WorkerCount => _workers.Length;
    public long TotalJobsEnqueued => _totalJobsEnqueued;
    public long TotalJobsProcessed => _workers.Sum(w => w.JobsProcessed);
    public int CurrentQueuedJobs => _workers.Sum(w => w.QueueLength);
    public float AverageJobTimeMs => _workers.Average(w => w.AverageJobTimeMs);
    public float MaxJobTimeMs => _workers.Max(w => w.MaxJobTimeMs);
    public WorkerStats[] GetWorkerStats() => _workers.Select(w => w.GetStats()).ToArray();

    public ThreadManager(int workerCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workerCount, 0);

        _workers = new Worker[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = new Worker($"WorkerThread_{i}");
        }
    }

    public void Enqueue(Action job)
    {
        var workerIndex = Interlocked.Increment(ref _nextWorkerIndex) % _workers.Length;
        _workers[workerIndex].Enqueue(job);
        Interlocked.Increment(ref _totalJobsEnqueued);
    }

    public void EnqueueBatch(IList<Action> jobs)
    {
        var jobCount = jobs.Count;
        if (jobCount == 0) return;

        var startIndex = Interlocked.Add(ref _nextWorkerIndex, jobCount) - jobCount;
        for (int i = 0; i < jobCount; i++)
        {
            var workerIndex = (startIndex + i) % _workers.Length;
            _workers[workerIndex].Enqueue(jobs[i]);
        }

        Interlocked.Add(ref _totalJobsEnqueued, jobCount);
    }

    public void Dispose()
    {
        foreach (var worker in _workers)
        {
            worker.Dispose();
        }
    }

    public struct WorkerStats
    {
        public string Name;
        public int QueueLength;
        public long JobsProcessed;
        public float AverageJobTimeMs;
        public float MaxJobTimeMs;
        public bool IsIdle;
    }

    private class Worker : IDisposable
    {
        private readonly Thread _thread;
        private readonly ConcurrentQueue<Action> _jobs = new();
        private readonly ManualResetEventSlim _wakeup = new(false);
        private readonly Stopwatch _timer = new();
        private readonly object _lock = new();
        private volatile bool _running = true;
        private volatile bool _isIdle = true;

        private long _jobsProcessed;
        private float _totalJobTimeMs;
        private float _maxJobTimeMs;

        public int QueueLength => _jobs.Count;
        public long JobsProcessed => _jobsProcessed;
        public float AverageJobTimeMs
        {
            get
            {
                lock (_lock)
                {
                    return _jobsProcessed > 0 ? _totalJobTimeMs / _jobsProcessed : 0;
                }
            }
        }
        public float MaxJobTimeMs
        {
            get
            {
                lock (_lock)
                {
                    return _maxJobTimeMs;
                }
            }
        }

        public Worker(string name)
        {
            _thread = new Thread(WorkLoop)
            {
                Name = name,
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _thread.Start();
        }

        public void Enqueue(Action job)
        {
            _jobs.Enqueue(job);
            _wakeup.Set();
        }

        public WorkerStats GetStats()
        {
            return new WorkerStats
            {
                Name = _thread.Name ?? Settings.NoName,
                QueueLength = QueueLength,
                JobsProcessed = JobsProcessed,
                AverageJobTimeMs = AverageJobTimeMs,
                MaxJobTimeMs = MaxJobTimeMs,
                IsIdle = _isIdle
            };
        }

        private void WorkLoop()
        {
            while (_running)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    _isIdle = false;
                    try
                    {
                        _timer.Restart();
                        job();
                        _timer.Stop();

                        var elapsed = (float)_timer.Elapsed.TotalMilliseconds;
                        lock (_lock)
                        {
                            _totalJobTimeMs += elapsed;
                            if (elapsed > _maxJobTimeMs)
                                _maxJobTimeMs = elapsed;
                        }

                        Interlocked.Increment(ref _jobsProcessed);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error executing job in Worker thread");
                    }
                }

                _isIdle = true;
                _wakeup.Wait();
                _wakeup.Reset();
            }
        }

        public void Dispose()
        {
            _running = false;
            _wakeup.Set();
        }
    }
}
