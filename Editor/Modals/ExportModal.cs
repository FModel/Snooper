using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.V2;
using Editor.Extensions;
using ImGuiNET;
using Serilog;
using Serilog.Events;
using Snooper;
using Snooper.UI;

namespace Editor.Modals;

public sealed class ExportModal
{
    public static ExportModal Instance { get; } = new();

    private const string Title = "Export Progress";
    private const string IconXMark = "\uf057";
    private const string IconFolder = "\uf07b";

    private readonly Vector4[] _pieColors =
    [
        new(0.22f, 0.52f, 0.90f, 1f),
        new(0.28f, 0.78f, 0.44f, 1f),
        new(0.90f, 0.62f, 0.22f, 1f),
        new(0.75f, 0.32f, 0.75f, 1f),
        new(0.32f, 0.75f, 0.85f, 1f),
        new(0.85f, 0.32f, 0.45f, 1f),
        new(0.90f, 0.90f, 0.22f, 1f),
        new(0.55f, 0.75f, 0.32f, 1f),
    ];

    private bool _openPopup;
    private bool _modalOpen;
    private bool _inProgress;
    private IReadOnlyList<ExportResult>? _exportResults;
    private CancellationTokenSource? _cts;

    private ExportProgress _currentProgress;
    private readonly IProgress<ExportProgress> _progress;
    private readonly Stopwatch _stopwatch = new();
    private readonly ConcurrentQueue<LogEvent> _pendingLogs = new();
    private readonly List<ClassGroup> _classGroups = [];

    private ExportModal()
    {
        ImGuiSink.Instance.OnExporterLogEvent += _pendingLogs.Enqueue;
        _progress = new Progress<ExportProgress>(p => _currentProgress = p);
    }

    public void Export(TreeNode node, string exportDirectory, ExporterOptions options)
    {
        Reset();
        _openPopup = true;
        _inProgress = true;
        _stopwatch.Restart();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                options.ExportMaterials = false; // we manually add materials
                var session = new ExportSession(exportDirectory, options);
                node.Export(session, token);
                _exportResults = await session.RunAsync(_progress, token);
            }
            catch (OperationCanceledException)
            {
                Log.Error("Export cancelled by user");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Export failed");
            }
            finally
            {
                _stopwatch.Stop();
                _inProgress = false;
            }
        }, token);
    }

    public void Draw()
    {
        if (_openPopup)
        {
            ImGui.OpenPopup(Title);
            _modalOpen = true;
            _openPopup = false;
        }

        if (!_modalOpen) return;

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(viewport.WorkSize * 0.75f, ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));

        var open = true;
        if (ImGui.BeginPopupModal(Title, ref open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
        {
            if (ImGui.BeginChild("##ModalInfoBody", Vector2.Zero, ImGuiChildFlags.FrameStyle))
            {
                DrawProgressBar();

                ImGui.Spacing();
                ImGui.SeparatorText("Export Log");
                DrawExportLog();
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }

        if (!open)
        {
            _modalOpen = false;
            Reset();
        }
    }

    private void Reset()
    {
        _pendingLogs.Clear();
        _classGroups.Clear();
        _exportResults = null;
        _currentProgress = new ExportProgress(0, 0);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void DrawProgressBar()
    {
        var e = _stopwatch.Elapsed;
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted("\uf017");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextUnformatted($"{e.Minutes:D2}:{e.Seconds:D2}.{e.Milliseconds / 10:D2}");

        if (!_inProgress && _exportResults is { Count: > 0 })
        {
            ImGui.SameLine();
            ImGui.TextColored(Settings.GreenColor, "\uf058");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_exportResults?.Count(r => r.Success) ?? 0} succeeded");

            ImGui.SameLine();
            ImGui.TextColored(Settings.RedColor, IconXMark);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_exportResults?.Count(r => !r.Success) ?? 0} failed");
        }

        ImGui.Spacing();

        var barColor = _classGroups.Any(cg => cg.ErrorCount > 0) ? new Vector4(0.75f, 0.32f, 0.32f, 1f) : _inProgress ? new Vector4(0.22f, 0.52f, 0.90f, 1f) : new Vector4(0.28f, 0.78f, 0.44f, 1f);
        var label = _inProgress && _currentProgress.Total > 0 ? _currentProgress.DisplayText : _inProgress ? "Preparing..." : "Done";

        var barPos = ImGui.GetCursorScreenPos();
        var barSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight());
        var hovered = ImGui.IsMouseHoveringRect(barPos, barPos + barSize);
        if (hovered && _inProgress)
        {
            barColor = new Vector4(0.75f, 0.32f, 0.32f, 1f);
            label = "\uf05e  Cancel";
        }

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
        ImGui.ProgressBar(_currentProgress.Percentage, barSize, label);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        if (_inProgress)
        {
            ImGui.SetCursorScreenPos(barPos);
            if (ImGui.InvisibleButton("##BarAction", barSize))
            {
                _cts?.Cancel();
            }
        }
    }

    private void DrawExportLog()
    {
        var avail = ImGui.GetContentRegionAvail();
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var canvasSize = 12 * rowH + ImGui.GetFrameHeightWithSpacing();
        var treeW = avail.X - canvasSize - ImGui.GetStyle().ItemSpacing.X;

        DrainPendingLogs();

        if (ImGui.BeginChild("##ExportLogTree", avail with { X = treeW }, ImGuiChildFlags.FrameStyle))
        {
            if (_classGroups.Count == 0)
            {
                ImGui.TextDisabled(_inProgress ? "Waiting for export data..." : "No export log.");
            }
            else for (var i = 0; i < _classGroups.Count; i++)
            {
                DrawClassGroup(i, _classGroups[i]);
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        DrawPieCanvas(canvasSize);
    }

    private void DrawPieCanvas(float size)
    {
        var canvasPos = ImGui.GetCursorScreenPos();
        var canvasVec = new Vector2(size, size);
        ImGui.InvisibleButton("##PieCanvas", canvasVec);
        var isHovered = ImGui.IsItemHovered();
        var mousePos = ImGui.GetMousePos();
        var dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(canvasPos, canvasPos + canvasVec, 0xFF_14_14_14);
        dl.AddRect(canvasPos, canvasPos + canvasVec, 0xFF_32_32_32);

        var total = _classGroups.Sum(cg => cg.Objects.Count);
        const float pad = 12f;
        var radius = size * 0.5f - pad;
        var center = canvasPos + new Vector2(size * 0.5f, size * 0.5f);

        if (total == 0 || radius <= 0)
        {
            dl.AddCircleFilled(center, MathF.Max(radius, 1f), 0xFF_1F_1F_1F, 64);
            return;
        }

        // Determine hovered slice by angle
        var hoveredSlice = -1;
        if (isHovered)
        {
            var dx = mousePos.X - center.X;
            var dy = mousePos.Y - center.Y;
            if (dx * dx + dy * dy <= radius * radius)
            {
                var angle = MathF.Atan2(dy, dx);
                while (angle < -MathF.PI / 2f) angle += MathF.PI * 2f;
                var cur = -MathF.PI / 2f;
                for (var i = 0; i < _classGroups.Count; i++)
                {
                    var sweep = (float)_classGroups[i].Objects.Count / total * MathF.PI * 2f;
                    if (angle >= cur && angle < cur + sweep) { hoveredSlice = i; break; }
                    cur += sweep;
                }
            }
        }

        float startAngle = -MathF.PI / 2f;
        for (var i = 0; i < _classGroups.Count; i++)
        {
            var cg = _classGroups[i];
            var ratio = (float) cg.Objects.Count / total;
            var sliceAngle = ratio * MathF.PI * 2f;
            var col = ImGui.GetColorU32(_pieColors[i % _pieColors.Length]);
            var r = i == hoveredSlice ? radius + 5f : radius;
            dl.PathLineTo(center);
            dl.PathArcTo(center, r, startAngle, startAngle + sliceAngle);
            dl.PathFillConvex(col);

            var midAngle = startAngle + sliceAngle * 0.5f;
            var labelPos = center + new Vector2(MathF.Cos(midAngle), MathF.Sin(midAngle)) * (radius * 0.62f);
            var pctStr = $"{ratio * 100f:F0}%";
            dl.AddText(labelPos - ImGui.CalcTextSize(pctStr) * 0.5f, 0xFF_FF_FF_FF, pctStr);

            startAngle += sliceAngle;
        }

        dl.AddCircle(center, radius, 0xAA_00_00_00, 64, 1.5f);

        if (hoveredSlice >= 0)
        {
            ImGui.BeginTooltip();

            var cg = _classGroups[hoveredSlice];
            ImGui.PushStyleColor(ImGuiCol.Text, _pieColors[hoveredSlice % _pieColors.Length]);
            ImGui.TextUnformatted("\uf111");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextUnformatted(cg.Name);

            ImGui.EndTooltip();
        }
    }

    private void DrawClassGroup(int index, ClassGroup cg)
    {
        var open = ImGui.CollapsingHeader($"{cg.Name}  ({cg.Objects.Count})##class_{cg.Name}");
        var headerMin = ImGui.GetItemRectMin();
        var headerMax = ImGui.GetItemRectMax();

        var labelW = MathF.Floor(ImGui.GetStyle().ItemSpacing.X * 0.5f);
        var col = ImGui.GetColorU32(_pieColors[index % _pieColors.Length]);
        ImGui.GetWindowDrawList().AddRectFilled(headerMin, headerMax with { X = headerMin.X + labelW }, col);

        if (cg.ErrorCount > 0 && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"{cg.ErrorCount} error{(cg.ErrorCount > 1 ? "s" : "")} in this class");
        }

        if (!open) return;
        foreach (var og in cg.Objects)
        {
            DrawObjectGroup(cg.Name, og);
        }
    }

    private void DrawObjectGroup(string className, ObjectGroup og)
    {
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;

        var hasErr = og.ErrorCount > 0;
        if (hasErr) ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
        var flags = ImGuiTreeNodeFlags.AllowOverlap | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding;
        var open = ImGui.TreeNodeEx($"{og.Name}##obj_{className}_{og.Name}", flags);
        if (hasErr) ImGui.PopStyleColor();

        if (og.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.FilePath)) is { } first)
        {
            var style = ImGui.GetStyle();
            var btnW = ImGui.CalcTextSize(IconFolder).X + style.FramePadding.X * 2;
            ImGui.SameLine(rightEdge - btnW);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            if (ImGui.Button($"{IconFolder}##obj_{className}_{og.Name}"))
            {
                OpenFileInExplorer(first.FilePath!);
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        if (!open) return;
        foreach (var entry in og.Entries)
        {
            DrawLogEntry(entry);
        }
        ImGui.TreePop();
    }

    private void DrawLogEntry(LogEntry entry)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, entry.Color);
        ImGui.TextUnformatted(entry.Icon);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextUnformatted(entry.Message);
        if (ImGui.IsItemHovered() && entry.Exception != null)
        {
            DrawExceptionTooltip(entry.Exception);
        }
    }

    private static void DrawExceptionTooltip(Exception ex)
    {
        ImGui.BeginTooltip();
        ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
        ImGui.TextUnformatted(ex.Message);
        ImGui.PopStyleColor();
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            ImGui.Separator();
            ImGui.TextWrapped(ex.StackTrace);
        }
        ImGui.EndTooltip();
    }

    private void OpenFileInExplorer(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            else Log.Warning("File not found: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file in Explorer: {FilePath}", filePath);
        }
    }

    private void DrainPendingLogs()
    {
        while (_pendingLogs.TryDequeue(out var log))
        {
            var className = log.GetContext("ClassName");
            var objectName = log.GetContext("ObjectName");
            var filePath = log.GetContext("FilePath");

            var cg = FindOrCreateClass(className);
            var og = FindOrCreateObject(cg, objectName);
            var entry = new LogEntry(log, filePath);
            if (entry.Icon == IconXMark)
            {
                og.ErrorCount++;
                cg.ErrorCount++;
            }
            og.Entries.Add(entry);
        }
    }

    private ClassGroup FindOrCreateClass(string name)
    {
        foreach (var cg in _classGroups)
            if (cg.Name == name) return cg;

        var n = new ClassGroup(name);
        _classGroups.Add(n);
        return n;
    }

    private static ObjectGroup FindOrCreateObject(ClassGroup cg, string name)
    {
        foreach (var og in cg.Objects)
            if (og.Name == name) return og;

        var n = new ObjectGroup(name);
        cg.Objects.Add(n);
        return n;
    }

    private sealed class LogEntry(LogEvent log, string? filePath)
    {
        public string Icon { get; } = log.Level switch
        {
            LogEventLevel.Error or LogEventLevel.Fatal => IconXMark,
            LogEventLevel.Warning => "\uf071",
            LogEventLevel.Information => "\uf05a",
            LogEventLevel.Debug => "\uf188",
            _ => "\uf5dc"
        };
        public Vector4 Color { get; } = log.Level switch
        {
            LogEventLevel.Error or LogEventLevel.Fatal => Settings.RedColor,
            LogEventLevel.Warning => Settings.YellowColor,
            LogEventLevel.Information => Settings.GreenColor,
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1f)
        };
        public string Message { get; } = $"[{log.Timestamp:HH:mm:ss.fff}] {log.RenderMessage()}";
        public string? FilePath { get; } = filePath;
        public Exception? Exception { get; } = log.Exception;
    }

    private sealed class ObjectGroup(string name)
    {
        public string Name { get; } = name;
        public List<LogEntry> Entries { get; } = [];
        public int ErrorCount { get; set; }
    }

    private sealed class ClassGroup(string name)
    {
        public string Name { get; } = name;
        public List<ObjectGroup> Objects { get; } = [];
        public int ErrorCount { get; set; }
    }
}
