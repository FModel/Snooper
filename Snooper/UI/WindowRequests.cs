namespace Snooper.UI;

public static class WindowRequests
{
    private static string? _pending;

    public static void Request(string title) => _pending = title;

    public static bool TryTake(out string title)
    {
        title = _pending ?? string.Empty;
        _pending = null;
        return title.Length > 0;
    }
}
