using System.Collections.Concurrent;
using System.Numerics;
using ImGuiNET;
using Serilog.Events;
using Editor.Managers;
using Snooper;
using Snooper.Core;

namespace Editor.Widgets;

/// <summary>
/// The in app console, fed by <see cref="ImGuiSink"/>. Serilog emits from whatever thread did the
/// logging, so events are queued and only turned into lines on the ui thread.
/// </summary>
public class LogWidget : PanelWidget
{
    public override string PanelTitle => Settings.LogWindow;
    public override PanelGroup Group => PanelGroup.Editor;

    private const string FollowIcon = "\uf103"; // angles-down

    private const int MaxLines = 4096;
    private const int TrimChunk = 1024; // trimming in chunks keeps the rebuild rare

    private const float SpineWidth = 2.5f; // the level colored edge running down the left of a row
    private const float RowPadX = 8f;
    private const float RowPadY = 2f;
    private const float ColumnGap = 12f;
    private const string MinLevelHint = "Shows this level and everything above it";

    private const float LevelWidth = 120f;
    private const float SearchWidth = 170f;
    private const float PanStep = 48f; // pixels per wheel notch when reading past the right edge

    private static readonly Vector4 TimeColor = new(0.42f, 0.46f, 0.52f, 1f);
    private static readonly Vector4 ControlColor = new(0.86f, 0.88f, 0.90f, 1f);

    private static readonly LogEventLevel[] _levels =
    [
        LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information,
        LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal
    ];

    private readonly struct Line(LogEventLevel level, string time, string text, float width, bool continuation)
    {
        public readonly LogEventLevel Level = level;
        public readonly string Time = time;
        public readonly string Text = text;

        /// <summary>Measured once on arrival, it decides how far the message column can pan.</summary>
        public readonly float Width = width;

        /// <summary>An exception line trailing its message, shown indented and dimmed.</summary>
        public readonly bool Continuation = continuation;
    }

    private readonly ConcurrentQueue<LogEvent> _pending = new();
    private readonly List<Line> _lines = [];
    private readonly List<int> _visible = [];
    private readonly int[] _counts = new int[_levels.Length];

    private LogEventLevel _minLevel = LogEventLevel.Debug;
    private string _search = string.Empty;
    private bool _autoScroll = true;
    private bool _lastParentVisible;
    private float _widestLine;
    private float _panX;
    private int _selected = -1;
    private int _stickToBottom;

    public LogWidget()
    {
        ImGuiSink.Instance.OnLogEvent += _pending.Enqueue;
    }

    // drained unconditionally, a closed window must not let the queue grow without bound
    protected override void Tick(EditorManager editor) => Drain();

    protected override void DrawContents(EditorManager editor)
    {
        DrawHeader();
        DrawLines();
    }

    private void Drain()
    {
        var appended = false;
        while (_pending.TryDequeue(out var log))
        {
            var time = log.Timestamp.ToString("HH:mm:ss.fff");
            appended |= Append(log.Level, time, $"[{SourceOf(log)}] {log.RenderMessage()}", false);

            if (log.Exception == null) continue;

            foreach (var line in log.Exception.ToString().Split('\n'))
            {
                appended |= Append(log.Level, time, line.TrimEnd('\r'), true);
            }
        }

        // the scroll max only catches up once the new rows have been laid out, so hold the
        // intent for a couple of frames instead of snapping against a stale extent
        if (appended && _autoScroll)
        {
            _stickToBottom = 2;
        }

        if (_lines.Count <= MaxLines) return;

        _lines.RemoveRange(0, Math.Max(TrimChunk, _lines.Count - MaxLines));
        _selected = -1;
        Rebuild();
    }

    private static string SourceOf(LogEvent log)
    {
        if (!log.Properties.TryGetValue("SourceContext", out var value) || value is not ScalarValue { Value: string name })
            return "Snooper";

        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    /// <summary>Returns whether the line ended up visible under the current filter.</summary>
    private bool Append(LogEventLevel level, string time, string text, bool continuation)
    {
        var line = new Line(level, time, text, ImGui.CalcTextSize(text).X, continuation);
        _lines.Add(line);
        _counts[(int) level]++;
        _widestLine = MathF.Max(_widestLine, line.Width);

        if (!continuation)
        {
            _lastParentVisible = Matches(line);
        }

        if (!_lastParentVisible) return false;

        _visible.Add(_lines.Count - 1);
        return true;
    }

    /// <summary>
    /// A continuation inherits its message's verdict, so an exception never gets orphaned
    /// from the line that explains it.
    /// </summary>
    private void Rebuild()
    {
        _visible.Clear();
        Array.Clear(_counts);
        _widestLine = 0f;

        var parentVisible = false;
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            _counts[(int) line.Level]++;
            _widestLine = MathF.Max(_widestLine, line.Width);

            if (!line.Continuation)
            {
                parentVisible = Matches(line);
            }

            if (parentVisible)
            {
                _visible.Add(i);
            }
        }

        _lastParentVisible = parentVisible;
    }

    private bool Matches(Line line)
    {
        if (line.Level < _minLevel) return false;

        return _search.Length == 0 || line.Text.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawHeader()
    {
        var dirty = false;

        // a tight frame padding keeps the strip one slim row instead of a toolbar
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 3f));

        // a threshold rather than six toggles, wanting only warnings is one click
        ImGui.SetNextItemWidth(LevelWidth);
        var open = ImGui.BeginCombo("##LogLevel", $"Min: {_minLevel}");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(MinLevelHint);

        if (open)
        {
            ImGui.TextDisabled(MinLevelHint);
            ImGui.Separator();

            for (var i = 0; i < _levels.Length; i++)
            {
                var level = _levels[i];

                ImGui.PushStyleColor(ImGuiCol.Text, LevelColor(level));
                var selected = ImGui.Selectable($"{LevelIcon(level)} {level}", _minLevel == level);
                ImGui.PopStyleColor();

                ImGui.SameLine();
                ImGui.TextDisabled($"{_counts[i]}");

                if (selected)
                {
                    _minLevel = level;
                    dirty = true;
                }
            }

            ImGui.EndCombo();
        }

        var height = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var block = SearchWidth + spacing + (height + spacing) * 3f;

        ImGui.SameLine();
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rightEdge - block));

        ImGui.SetNextItemWidth(SearchWidth);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1f, 1f, 1f, 0.04f));
        if (ImGui.InputTextWithHint("##LogSearch", "Filter", ref _search, 128))
        {
            dirty = true;
        }
        ImGui.PopStyleColor();

        ImGui.SameLine();
        if (IconButton("##follow", FollowIcon, ControlColor, _autoScroll, "Follow the tail"))
        {
            _autoScroll = !_autoScroll;
            _stickToBottom = _autoScroll ? 2 : 0;
        }

        ImGui.SameLine();
        if (IconButton("##copy", Settings.CopyIcon, ControlColor, false, _selected >= 0 ? "Copy the selected line" : "Copy every visible line"))
        {
            Copy();
        }

        ImGui.SameLine();
        if (IconButton("##clear", Settings.TrashIcon, ControlColor, false, "Clear"))
        {
            _lines.Clear();
            _visible.Clear();
            Array.Clear(_counts);
            _widestLine = 0f;
            _selected = -1;
            _panX = 0f;
        }

        ImGui.PopStyleVar(2);

        if (dirty)
        {
            _selected = -1;
            Rebuild();
        }
    }

    private void DrawLines()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.22f));
        var visible = ImGui.BeginChild("##LogScroll", Vector2.Zero);
        ImGui.PopStyleColor();

        if (!visible)
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
            return;
        }

        var rowHeight = ImGui.GetTextLineHeight() + RowPadY * 2f;
        var timeWidth = ImGui.CalcTextSize("00:00:00.000").X;
        var messageX = RowPadX + SpineWidth + ColumnGap + timeWidth + ColumnGap;
        var width = ImGui.GetContentRegionAvail().X;

        // no scrollbar for the overflow, the message column pans under the wheel instead
        var maxPan = MathF.Max(0f, messageX + _widestLine + RowPadX - width);
        if (ImGui.IsWindowHovered())
        {
            var io = ImGui.GetIO();
            var wheel = io.MouseWheelH + (io.KeyShift ? io.MouseWheel : 0f);
            if (wheel != 0f)
            {
                _panX -= wheel * PanStep;
            }
        }
        _panX = Math.Clamp(_panX, 0f, maxPan);

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_visible.Count, rowHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    DrawRow(i, width, rowHeight, messageX);
                }
            }

            clipper.End();
            clipper.Destroy();
        }

        if (_stickToBottom > 0)
        {
            ImGui.SetScrollY(ImGui.GetScrollMaxY());
            _stickToBottom--;
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    private void DrawRow(int index, float width, float height, float messageX)
    {
        var line = _lines[_visible[index]];
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        if (ImGui.InvisibleButton($"##row{index}", new Vector2(width, height)))
        {
            _selected = _selected == index ? -1 : index;
        }

        var max = origin + new Vector2(width, height);
        var color = LevelColor(line.Level);

        if (line.Level == LogEventLevel.Fatal)
        {
            drawList.AddRectFilled(origin, max, ImGui.GetColorU32(color with { W = 0.16f }));
        }

        if (_selected == index)
        {
            drawList.AddRectFilled(origin, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)));
        }
        else if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(origin, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));
        }

        var textY = origin.Y + RowPadY;

        // the message is the only thing that pans, so it must not slide under the timestamp
        drawList.PushClipRect(new Vector2(origin.X + messageX, origin.Y), max, true);
        var indent = line.Continuation ? ColumnGap : 0f;
        var messageColor = line.Continuation ? TimeColor : MessageColor(line.Level);
        drawList.AddText(new Vector2(origin.X + messageX + indent - _panX, textY), ImGui.GetColorU32(messageColor), line.Text);
        drawList.PopClipRect();

        // a continuation belongs to the message above it, no spine and no timestamp of its own
        if (line.Continuation) return;

        var spineX = origin.X + RowPadX;
        drawList.AddRectFilled(new Vector2(spineX, origin.Y + 1f), new Vector2(spineX + SpineWidth, max.Y - 1f), ImGui.GetColorU32(color));
        drawList.AddText(new Vector2(spineX + SpineWidth + ColumnGap, textY), ImGui.GetColorU32(TimeColor), line.Time);
    }

    private void Copy()
    {
        var lines = _selected >= 0 && _selected < _visible.Count
            ? [_lines[_visible[_selected]]]
            : _visible.Select(i => _lines[i]);

        ImGui.SetClipboardText(string.Join('\n', lines.Select(l => l.Continuation ? $"    {l.Text}" : $"[{l.Time}] {l.Text}")));
        Notifications.Push("log.copy", Settings.CopyIcon, "Copied to clipboard");
    }

    /// <summary>
    /// Frameless square toggle, the default button chrome would drown a strip this small.
    /// </summary>
    private static bool IconButton(string id, string label, Vector4 color, bool active, string tooltip)
    {
        var size = new Vector2(ImGui.GetFrameHeight());
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        if (active || hovered)
        {
            drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(color with { W = active ? 0.18f : 0.08f }));
        }

        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(origin + (size - textSize) * 0.5f, ImGui.GetColorU32(active || hovered ? color : color with { W = 0.45f }), label);

        if (hovered) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private static string LevelIcon(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal or LogEventLevel.Error => "\uf057",  // circle-xmark
        LogEventLevel.Warning => "\uf071",  // triangle-exclamation
        LogEventLevel.Information => "\uf05a",  // circle-info
        LogEventLevel.Debug => "\uf188",  // bug
        _ => "\uf5dc"   // brain
    };

    /// <summary>
    /// Saturated, because the spine and the dropdown are what carry the level at a glance.
    /// Same amber and brick as the hardware band, so a warning reads the same everywhere.
    /// </summary>
    private static Vector4 LevelColor(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal => new Vector4(0.95f, 0.30f, 0.50f, 1f),
        LogEventLevel.Error => new Vector4(0.88f, 0.35f, 0.32f, 1f),
        LogEventLevel.Warning => new Vector4(0.95f, 0.72f, 0.28f, 1f),
        LogEventLevel.Information => new Vector4(0.78f, 0.82f, 0.87f, 1f),
        LogEventLevel.Debug => new Vector4(0.48f, 0.54f, 0.62f, 1f),
        _ => new Vector4(0.36f, 0.40f, 0.46f, 1f)
    };

    /// <summary>
    /// Softened against <see cref="LevelColor"/>, a wall of fully saturated text is unreadable.
    /// </summary>
    private static Vector4 MessageColor(LogEventLevel level) => level switch
    {
        LogEventLevel.Fatal => new Vector4(0.97f, 0.62f, 0.74f, 1f),
        LogEventLevel.Error => new Vector4(0.93f, 0.60f, 0.56f, 1f),
        LogEventLevel.Warning => new Vector4(0.93f, 0.80f, 0.55f, 1f),
        LogEventLevel.Information => new Vector4(0.84f, 0.86f, 0.89f, 1f),
        LogEventLevel.Debug => new Vector4(0.58f, 0.62f, 0.68f, 1f),
        _ => new Vector4(0.44f, 0.48f, 0.54f, 1f)
    };
}
