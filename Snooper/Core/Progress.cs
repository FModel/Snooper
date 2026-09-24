namespace Snooper.Core;

public static class Progress
{
    public const float Linger = 0.8f; // how long a finished bar stays, so short work is still seen

    public sealed class Operation(string key, string icon, string text)
    {
        public readonly string Key = key;
        public string Icon = icon;
        public string Text = text;
        public int Done;
        public int Total;
        public float Age; // since it finished
        public string? Completed; // what the bar turns into, as a notification, when it goes away

        public bool IsFinished => Done >= Total;
        public float Fraction => Total > 0 ? Math.Clamp((float) Done / Total, 0f, 1f) : 1f;
    }

    private static readonly List<Operation> _active = [];
    public static IReadOnlyList<Operation> Active => _active;

    public static void Report(string key, string icon, string text, int done, int total, string? completed = null)
    {
        Operation? operation = null;
        foreach (var active in _active)
        {
            if (active.Key != key) continue;

            operation = active;
            break;
        }

        if (operation is null)
        {
            operation = new Operation(key, icon, text);
            _active.Add(operation);
        }

        operation.Icon = icon;
        operation.Text = text;
        operation.Done = done;
        operation.Total = total;
        operation.Completed = completed;
        if (!operation.IsFinished) operation.Age = 0f;
    }

    public static void Clear() => _active.Clear();

    public static void Advance(float delta)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var operation = _active[i];
            if (!operation.IsFinished) continue;

            operation.Age += delta;
            if (operation.Age < Linger) continue;

            _active.RemoveAt(i);
            if (operation.Completed is { } completed)
                Notifications.Push(operation.Key, operation.Icon, completed);
        }
    }
}
