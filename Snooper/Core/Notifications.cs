namespace Snooper.Core;

public static class Notifications
{
    public const float Lifetime = 1.6f;

    private const int MaxVisible = 4;

    public sealed class Notification(string key, string icon, string text)
    {
        public readonly string Key = key;
        public string Icon = icon;
        public string Text = text;
        public float Age;
    }

    private static readonly List<Notification> _active = [];
    public static IReadOnlyList<Notification> Active => _active;

    public static void Push(string key, string text) => Push(key, string.Empty, text);
    public static void Push(string key, string icon, string text)
    {
        foreach (var notification in _active)
        {
            if (notification.Key != key) continue;

            notification.Icon = icon;
            notification.Text = text;
            notification.Age = 0f;
            return;
        }

        if (_active.Count == MaxVisible)
        {
            _active.RemoveAt(0);
        }

        _active.Add(new Notification(key, icon, text));
    }

    /// <summary>
    /// Ages the messages and drops the expired ones, driven by whoever renders them.
    /// </summary>
    public static void Advance(float delta)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            _active[i].Age += delta;
            if (_active[i].Age >= Lifetime)
            {
                _active.RemoveAt(i);
            }
        }
    }
}
