namespace Snooper.UI;

public static class WindowRequests
{
    private static string? _pending;
    private static readonly Dictionary<string, object> _payloads = [];

    public static void Request(string title, object? payload = null)
    {
        _pending = title;
        if (payload is null)
            _payloads.Remove(title);
        else
            _payloads[title] = payload;
    }

    public static bool TryTake(out string title)
    {
        title = _pending ?? string.Empty;
        _pending = null;
        return title.Length > 0;
    }

    public static object? TakePayload(string title) => _payloads.Remove(title, out var payload) ? payload : null;

    public static void Clear() => _payloads.Clear();
}
