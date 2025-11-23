using System.Collections.Concurrent;
using System.Numerics;
using ImGuiNET;
using Serilog.Events;

namespace Snooper.UI;

public static class LogWindow
{
    private const int MaxLogEntries = 1000;

    private static readonly ConcurrentQueue<LogEvent> LogEntries = new();
    private static readonly List<LogEvent> DisplayEntries = [];

    private static LogEventLevel _minLevel = LogEventLevel.Verbose;
    private static string _filterText = string.Empty;
    private static bool _autoScroll = true;
    private static bool _scrollToBottom;

    public static void AddLog(LogEvent logEvent)
    {
        LogEntries.Enqueue(logEvent);
        
        while (LogEntries.Count > MaxLogEntries)
        {
            LogEntries.TryDequeue(out _);
        }
    }

    public static void Draw()
    {
        if (!ImGui.Begin("Log"))
        {
            ImGui.End();
            return;
        }

        while (LogEntries.TryDequeue(out var entry))
        {
            DisplayEntries.Add(entry);
            if (_autoScroll)
                _scrollToBottom = true;
        }
        
        while (DisplayEntries.Count > MaxLogEntries)
        {
            DisplayEntries.RemoveAt(0);
        }
        
        DrawControls();
        ImGui.Separator();
        DrawLogEntries();
        
        ImGui.End();
    }

    private static void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##filter", "Filter...", ref _filterText, 256);
        
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(120);
        if (ImGui.BeginCombo("##level", _minLevel.ToString()))
        {
            foreach (var level in Enum.GetValues<LogEventLevel>())
            {
                var isSelected = _minLevel == level;
                if (ImGui.Selectable(level.ToString(), isSelected))
                {
                    _minLevel = level;
                }
                
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        
        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);
        
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            DisplayEntries.Clear();
        }
        
        ImGui.SameLine();
        ImGui.Text($"({DisplayEntries.Count} entries)");
    }

    private static void DrawLogEntries()
    {
        if (!ImGui.BeginChild("LogEntries", Vector2.Zero, ImGuiChildFlags.Borders))
        {
            ImGui.EndChild();
            return;
        }
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));
        
        foreach (var entry in DisplayEntries)
        {
            if (entry.Level < _minLevel) continue;
            
            if (!string.IsNullOrWhiteSpace(_filterText))
            {
                var message = entry.RenderMessage();
                if (!message.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            
            DrawLogEntry(entry);
        }
        
        ImGui.PopStyleVar();
        
        if (_scrollToBottom)
        {
            ImGui.SetScrollHereY(1.0f);
            _scrollToBottom = false;
        }
        
        ImGui.EndChild();
    }

    private static void DrawLogEntry(LogEvent entry)
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"[{entry.Timestamp:T}] ");
        ImGui.SameLine();
        ImGui.TextColored(GetLevelColor(entry.Level), $"[{GetLevelShortName(entry.Level)}] ");
        ImGui.SameLine();
        ImGui.TextUnformatted(entry.RenderMessage());
        
        if (entry.Exception != null)
        {
            ImGui.Indent();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
            ImGui.TextWrapped(entry.Exception.ToString());
            ImGui.PopStyleColor();
            ImGui.Unindent();
        }
    }

    private static Vector4 GetLevelColor(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
            LogEventLevel.Debug => new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
            LogEventLevel.Information => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),
            LogEventLevel.Warning => new Vector4(1.0f, 0.8f, 0.0f, 1.0f),
            LogEventLevel.Error => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),
            LogEventLevel.Fatal => new Vector4(1.0f, 0.0f, 0.5f, 1.0f),
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }

    private static string GetLevelShortName(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "???"
        };
    }
}
