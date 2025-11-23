using Serilog.Core;
using Serilog.Events;

namespace Snooper.UI;

public class ImGuiSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        LogWindow.AddLog(logEvent);
    }
}

