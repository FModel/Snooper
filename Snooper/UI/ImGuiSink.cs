using Serilog.Core;
using Serilog.Events;
using Snooper.UI.Widgets;

namespace Snooper.UI;

public class ImGuiSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        LogsViewer.AddLog(logEvent);
    }
}

