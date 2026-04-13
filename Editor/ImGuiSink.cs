using Serilog.Core;
using Serilog.Events;

namespace Editor;

public class ImGuiSink : ILogEventSink
{
    public static ImGuiSink Instance { get; } = new();

    private ImGuiSink()
    {

    }

    public event Action<LogEvent>? OnLogEvent;
    public event Action<LogEvent>? OnExporterLogEvent;

    public void Emit(LogEvent logEvent)
    {
        OnLogEvent?.Invoke(logEvent);

        if (logEvent.Properties.TryGetValue("ExporterV2", out var state) && state is ScalarValue { Value: true })
        {
            OnExporterLogEvent?.Invoke(logEvent);
        }
    }
}
