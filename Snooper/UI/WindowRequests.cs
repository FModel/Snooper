namespace Snooper.UI;

public static class WindowRequests
{
    private static string? _pending;
    private static readonly Dictionary<string, object> _payloads = [];

    public static void Request(string title, object? payload = null)
    {
        _pending = title;
        if (payload is not null) _payloads[title] = payload;
    }

    public static bool TryTake(out string title)
    {
        title = _pending ?? string.Empty;
        _pending = null;
        return title.Length > 0;
    }

    public static T? GetPayload<T>(string title) where T : class => _payloads.TryGetValue(title, out var payload) ? payload as T : null;

    public static void ClearPayloads() => _payloads.Clear();
}
