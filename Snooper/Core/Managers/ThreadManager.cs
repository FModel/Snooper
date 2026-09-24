using System.Collections.Concurrent;
using Serilog;

namespace Snooper.Core.Managers;

public static class ThreadManager
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(ThreadManager));

    private static readonly ConcurrentQueue<Action> _jobs = new();
    private static readonly SemaphoreSlim _available = new(0); // released once per job, what the workers sleep on
    private static long _enqueued;
    private static long _finished; // ran to the end or failed
    private static int _busy;

    // last: the workers start pulling right away, from everything above
    private static readonly Thread[] _workers = StartWorkers(Math.Max(1, Environment.ProcessorCount - 2));

    public static int WorkerCount => _workers.Length;
    public static int BusyWorkers => Volatile.Read(ref _busy);
    public static int CurrentQueuedJobs => _jobs.Count;
    public static long TotalJobsProcessed => Interlocked.Read(ref _finished);

    public static void Enqueue(Action job)
    {
        Interlocked.Increment(ref _enqueued); // before the job can be taken, so finished never runs ahead of enqueued
        _jobs.Enqueue(job);
        _available.Release();
    }

    public static void ClearAndDispose()
    {
        while (_jobs.TryDequeue(out _))
            Interlocked.Decrement(ref _enqueued);

        _batchStart = Interlocked.Read(ref _enqueued); // whatever was going on is not worth a bar anymore
    }

    private const int MinReportedBatch = 8;
    private static long _batchStart; // jobs finished before the current batch, render thread only

    public static void Update()
    {
        var finished = Interlocked.Read(ref _finished); // read first, enqueued can only be ahead of it
        var enqueued = Interlocked.Read(ref _enqueued);

        var total = (int) (enqueued - _batchStart);
        if (total <= 0) return;

        var done = (int) Math.Max(0, finished - _batchStart);
        if (total >= MinReportedBatch)
        {
            var completed = done == total ? $"{total:N0} jobs processed" : null;
            Progress.Report("work.jobs", Settings.JobIcon, "Processing jobs", done, total, completed);
        }

        if (done == total)
            _batchStart = enqueued;
    }

    private static Thread[] StartWorkers(int count)
    {
        var workers = new Thread[count];
        for (var i = 0; i < count; i++)
        {
            workers[i] = new Thread(Work)
            {
                Name = $"WorkerThread_{i}",
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            workers[i].Start();
        }

        return workers;
    }

    private static void Work()
    {
        while (true)
        {
            _available.Wait();
            if (!_jobs.TryDequeue(out var job)) continue; // the queue was cleared in between

            Interlocked.Increment(ref _busy);
            try
            {
                job();
            }
            catch (Exception e)
            {
                Log.Error(e, "Job failed on {Worker}", Thread.CurrentThread.Name);
            }
            finally
            {
                Interlocked.Decrement(ref _busy);
                Interlocked.Increment(ref _finished);
            }
        }
    }
}
