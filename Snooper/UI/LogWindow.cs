using System.Collections.Concurrent;
using System.Numerics;
using ImGuiNET;
using Serilog.Events;

namespace Snooper.UI;

public static class LogWindow
{
    private const int MaxLogEntries = 10000;

    private static readonly ConcurrentQueue<LogEvent> _logEntries = new();
    private static readonly List<LogEvent> _displayEntries = [];
    private static readonly List<LogEvent> _filteredEntries = [];

    private static LogEventLevel _minLevel = LogEventLevel.Verbose;
    private static string _filterText = string.Empty;
    private static bool _autoScroll = true;
    private static bool _scrollToBottom;
    private static bool _needsRefilter;

    public static void AddLog(LogEvent logEvent)
    {
        _logEntries.Enqueue(logEvent);

        while (_logEntries.Count > MaxLogEntries)
        {
            _logEntries.TryDequeue(out _);
        }
    }

    public static void Draw()
    {
        if (!ImGui.Begin("Log"))
        {
            ImGui.End();
            return;
        }

        while (_logEntries.TryDequeue(out var entry))
        {
            _displayEntries.Add(entry);
            if (_autoScroll)
            {
                _scrollToBottom = true;
            }
            _needsRefilter = true;
        }

        while (_displayEntries.Count > MaxLogEntries)
        {
            _displayEntries.RemoveAt(0);
            _needsRefilter = true;
        }

        if (_needsRefilter)
        {
            RebuildFilteredEntries();
            _needsRefilter = false;
        }

        DrawControls();
        ImGui.Separator();
        DrawLogEntries();

        ImGui.End();
    }

    private static bool NeedsFiltering()
    {
        return _minLevel != LogEventLevel.Verbose || !string.IsNullOrWhiteSpace(_filterText);
    }

    private static void RebuildFilteredEntries()
    {
        _filteredEntries.Clear();

        if (!NeedsFiltering())
        {
            return;
        }

        foreach (var entry in _displayEntries)
        {
            if (entry.Level < _minLevel) continue;

            if (!string.IsNullOrWhiteSpace(_filterText))
            {
                var message = entry.RenderMessage();
                if (!message.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            _filteredEntries.Add(entry);
        }
    }

    private static void DrawControls()
    {
        ImGui.SetNextItemWidth(200);
        _needsRefilter = ImGui.InputTextWithHint("##filter", "Filter...", ref _filterText, 256);

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
                    _needsRefilter = true;
                    _scrollToBottom = true;
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
            _displayEntries.Clear();
            _filteredEntries.Clear();
        }

        ImGui.SameLine();
        ImGui.Text($"({(NeedsFiltering() ? _filteredEntries : _displayEntries).Count} entries)");
    }

    private static void DrawLogEntries()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.WindowBg));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 2));

        if (ImGui.BeginChild("LogEntries", Vector2.Zero, ImGuiChildFlags.Borders))
        {
            unsafe
            {
                var entriesToRender = NeedsFiltering() ? _filteredEntries : _displayEntries;
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(entriesToRender.Count);

                while (clipper.Step())
                {
                    for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        DrawLogEntry(entriesToRender[i]);
                    }
                }

                clipper.End();
                clipper.Destroy();
            }

            if (_scrollToBottom)
            {
                ImGui.SetScrollHereY(1.0f);
                _scrollToBottom = false;
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private static void DrawLogEntry(LogEvent entry)
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"[{entry.Timestamp:T}] ");
        ImGui.SameLine();
        ImGui.TextColored(GetLevelColor(entry.Level), $"[{GetLevelShortName(entry.Level)}] ");
        ImGui.SameLine();
        ImGui.TextUnformatted(entry.RenderMessage());

        // if (entry.Exception != null)
        // {
        //     ImGui.Indent();
        //     ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
        //     ImGui.TextUnformatted(entry.Exception.ToString());
        //     ImGui.PopStyleColor();
        //     ImGui.Unindent();
        // }
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
