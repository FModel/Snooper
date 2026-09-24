using System.Diagnostics;

namespace Snooper.Core.Managers;

public sealed class FrameBudget
{
    private long _start;
    private long _limit;

    public float BudgetMs { get; private set; }
    public float ElapsedMs => (float) Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
    public bool Exhausted => Stopwatch.GetTimestamp() - _start >= _limit;

    public bool Spent(float share) => Stopwatch.GetTimestamp() - _start >= (long) (_limit * share);

    public void Begin(float budgetMs = 4.0f)
    {
        BudgetMs = budgetMs;
        _start = Stopwatch.GetTimestamp();
        _limit = (long) (BudgetMs / 1000.0 * Stopwatch.Frequency);
    }
}
