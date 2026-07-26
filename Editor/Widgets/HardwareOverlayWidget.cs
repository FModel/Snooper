using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Hardware;
using Snooper.Core.Managers;
using Snooper.Extensions;
using Snooper.Rendering.Cache;

namespace Editor.Widgets;

/// <summary>
/// Dense hardware readout drawn as a black band flush against the bottom of the viewport:
/// tightly packed cells of label/value rows that flow left to right and wrap to fill the width.
/// </summary>
public class HardwareOverlayWidget
{
    private const float FontScale = 0.85f; // relative to the UI font, so the band follows the DPI scale
    private const float PadX = 8f;         // padding inside the band
    private const float PadY = 6f;
    private const float CellGap = 18f;     // horizontal gap between two cells
    private const float BandGap = 8f;      // vertical gap between two wrapped bands
    private const float LabelGap = 8f;     // minimum gap between a label and its value
    private const float BarWidth = 54f;
    private const float BarHeight = 6f;

    private static readonly uint BandColor = Color(0f, 0f, 0f, 0.75f);
    private static readonly uint BorderColor = Color(1f, 1f, 1f, 0.12f);
    private static readonly uint SeparatorColor = Color(1f, 1f, 1f, 0.06f);
    private static readonly uint LabelColor = Color(0.42f, 0.46f, 0.52f);
    private static readonly uint ValueColor = Color(0.86f, 0.88f, 0.90f);
    private static readonly uint AccentColor = Color(0.40f, 0.78f, 0.95f);
    private static readonly uint WarnColor = Color(0.95f, 0.75f, 0.25f);
    private static readonly uint AlertColor = Color(0.92f, 0.82f, 0.18f);
    private static readonly uint AlertTextColor = Color(0.05f, 0.05f, 0.05f);

    private enum Severity
    {
        None,

        /// <summary>Value is tinted, the way a nearly exhausted budget reads.</summary>
        Warn,

        /// <summary>Value gets a filled background, the way a blown budget reads.</summary>
        Alert
    }

    private readonly struct Row(string label, string value, uint color, Severity severity, float fraction)
    {
        public readonly string Label = label;
        public readonly string Value = value;
        public readonly uint Color = color;
        public readonly Severity Severity = severity;

        /// <summary>Negative for a plain row, otherwise the fill ratio of an inline usage bar.</summary>
        public readonly float Fraction = fraction;

        public bool IsBar => Fraction >= 0f;
    }

    private sealed class Cell
    {
        public readonly List<Row> Rows = [];
        public float Width;
        public Vector2 Position;
        public float BandHeight;
        public bool IsBandStart;
    }

    private readonly List<Cell> _cells = [];
    private int _cellCount;
    private ImFontPtr _font;
    private float _fontSize;
    private float _rowHeight;

    /// <summary>
    /// Draws the band and returns the height it occupies, so the other overlays can clear it.
    /// </summary>
    public float Draw(ImDrawListPtr drawList, Vector2 contentPos, Vector2 contentSize, ActorManager manager)
    {
        if (!RendererInfo.TrackMemory) return 0f;

        _font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        _fontSize = ImGui.GetFontSize() * FontScale;
        _rowHeight = MathF.Round(_fontSize * 1.15f);

        _cellCount = 0;
        Build(manager);
        Measure();

        // A viewport this small has nothing to spare, and the band would swallow the whole view.
        var maxHeight = contentSize.Y * 0.4f;
        if (maxHeight < _rowHeight * 3f) return 0f;

        var contentHeight = Layout(contentSize.X - PadX * 2f, maxHeight - PadY * 2f);
        if (_cellCount == 0) return 0f;

        var height = contentHeight + PadY * 2f;
        var top = contentPos.Y + contentSize.Y - height;

        drawList.AddRectFilled(contentPos with { Y = top }, contentPos + contentSize, BandColor);
        drawList.AddLine(contentPos with { Y = top }, new Vector2(contentPos.X + contentSize.X, top), BorderColor);

        DrawCells(drawList, new Vector2(contentPos.X + PadX, top + PadY), contentSize.X - PadX * 2f);

        return height;
    }

    private void Build(ActorManager manager)
    {
        var renderer = manager.Renderer;
        var gpu = renderer.DeviceInfo.Memory;
        var ram = renderer.SystemMemory;

        var device = BeginCell();
        AddRow(device, "GPU", renderer.DeviceInfo.Name, AccentColor);
        AddRow(device, "API", renderer.Name, AccentColor);
        AddRow(device, "VND", renderer.DeviceInfo.Vendor, AccentColor);

        if (gpu.IsAvailable)
        {
            var used = gpu.UsedBytes;
            var total = gpu.TotalBytes;
            var ratio = (float) used / total;

            var vram = BeginCell();
            AddBar(vram, "VRAM", ratio, $"{ratio * 100f:F1}%");
            AddRow(vram, "USED", used.GetReadableSize(), Pressure(ratio));
            AddRow(vram, "TOTAL", $"{(gpu.IsTotalEstimated ? "~" : string.Empty)}{total.GetReadableSize()}");

            var detail = BeginCell();
            AddRow(detail, "FREE", gpu.AvailableBytes.GetReadableSize());
            if (gpu.DedicatedBytes > 0)
                AddRow(detail, "BOARD", gpu.DedicatedBytes.GetReadableSize());
            if (gpu.EvictionCount > 0)
                AddRow(detail, "EVICT", $"{gpu.EvictedBytes.GetReadableSize()} ({gpu.EvictionCount:N0}x)", Severity.Alert);
        }
        else
        {
            var vram = BeginCell();
            AddRow(vram, "VRAM", "unsupported", Severity.Warn);
            AddRow(vram, "NEEDS", "NVX_gpu_memory_info");
            AddRow(vram, "OR", "ATI_meminfo");
        }

        var systemRatio = ram.TotalBytes > 0 ? (float) ram.UsedBytes / ram.TotalBytes : 0f;
        var system = BeginCell();
        AddBar(system, "RAM", systemRatio, $"{systemRatio * 100f:F1}%");
        AddRow(system, "USED", ram.UsedBytes.GetReadableSize(), Pressure(systemRatio));
        AddRow(system, "TOTAL", ram.TotalBytes.GetReadableSize());

        var process = BeginCell();
        AddRow(process, "PROC", ram.ProcessBytes.GetReadableSize());
        AddRow(process, "HEAP", ram.ManagedBytes.GetReadableSize());
        AddRow(process, "GC", $"{GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");

        var allocated = manager.Allocated;
        var wasted = allocated - manager.Used;
        var bufferRatio = allocated > 0 ? (float) manager.Used / allocated : 0f;
        var buffers = BeginCell();
        AddBar(buffers, "BUF", bufferRatio, $"{bufferRatio * 100f:F1}%");
        AddRow(buffers, "ALLOC", allocated.GetReadableSize());
        AddRow(buffers, "WASTE", wasted.GetReadableSize(), allocated > 0 && (float) wasted / allocated > 0.3f ? Severity.Warn : Severity.None);

        var io = ImGui.GetIO();
        var frame = BeginCell();
        AddRow(frame, "FPS", $"{io.Framerate:F1}", io.Framerate < 30f ? Severity.Warn : Severity.None);
        AddRow(frame, "FRAME", $"{io.DeltaTime * 1000f:F2} ms");

        var frameNode = Profiler.Enabled && Profiler.Root.Children.Count > 1 ? Profiler.Root.Children[1] : null;
        if (frameNode != null)
        {
            AddRow(frame, "CPU/GPU", $"{frameNode.Cpu.AverageTimeElapsedMs:F2} / {frameNode.Gpu.AverageTimeElapsedMs:F2}");
        }

        var scene = BeginCell();
        AddRow(scene, "ACTORS", $"{manager.ActorCount:N0}");
        AddRow(scene, "PRIMS", Profiler.Enabled ? $"{Profiler.TotalPrimitives:N0}" : "--");
        AddRow(scene, "TEX", $"{TextureCache.LoadedTextureCount:N0} +{TextureCache.PendingTextureCount:N0}");

        var threads = manager.ThreadManager;
        var jobs = BeginCell();
        AddRow(jobs, "WORKERS", $"{threads.WorkerCount}");
        AddRow(jobs, "QUEUED", $"{threads.CurrentQueuedJobs:N0}", threads.CurrentQueuedJobs > 0 ? Severity.Warn : Severity.None);
        AddRow(jobs, "DONE", $"{threads.TotalJobsProcessed:N0}");
    }

    private void Measure()
    {
        for (var i = 0; i < _cellCount; i++)
        {
            var cell = _cells[i];
            var width = 0f;

            foreach (var row in cell.Rows)
            {
                var rowWidth = TextWidth(row.Label) + LabelGap + TextWidth(row.Value);
                if (row.IsBar) rowWidth += BarWidth + LabelGap;

                width = MathF.Max(width, rowWidth);
            }

            cell.Width = width;
        }
    }

    /// <summary>
    /// Flows the cells into bands, dropping whatever no longer fits, and returns the total height.
    /// </summary>
    private float Layout(float width, float maxHeight)
    {
        var x = 0f;
        var y = 0f;
        var bandStart = 0;

        for (var i = 0; i < _cellCount; i++)
        {
            var cell = _cells[i];
            if (i > bandStart && x + cell.Width > width)
            {
                var previous = CloseBand(bandStart, i, y);
                if (y + previous + BandGap + _rowHeight > maxHeight)
                {
                    // the next band would overflow, so this is where the readout stops
                    _cellCount = i;
                    return y + previous;
                }

                y += previous + BandGap;
                x = 0f;
                bandStart = i;
            }

            cell.Position.X = x;
            cell.IsBandStart = i == bandStart;
            x += cell.Width + CellGap;
        }

        return y + CloseBand(bandStart, _cellCount, y);
    }

    private float CloseBand(int start, int end, float y)
    {
        var rows = 0;
        for (var i = start; i < end; i++)
        {
            rows = Math.Max(rows, _cells[i].Rows.Count);
        }

        var height = rows * _rowHeight;
        for (var i = start; i < end; i++)
        {
            _cells[i].Position.Y = y;
            _cells[i].BandHeight = height;
        }

        return height;
    }

    private void DrawCells(ImDrawListPtr drawList, Vector2 origin, float width)
    {
        for (var i = 0; i < _cellCount; i++)
        {
            var cell = _cells[i];
            var position = origin + cell.Position;

            if (cell.IsBandStart)
            {
                if (i > 0)
                {
                    var y = position.Y - BandGap * 0.5f;
                    drawList.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width, y), SeparatorColor);
                }
            }
            else
            {
                var x = position.X - CellGap * 0.5f;
                drawList.AddLine(new Vector2(x, position.Y), new Vector2(x, position.Y + cell.BandHeight), SeparatorColor);
            }

            for (var r = 0; r < cell.Rows.Count; r++)
            {
                DrawRow(drawList, cell.Rows[r], position.X, position.Y + r * _rowHeight, cell.Width);
            }
        }
    }

    private void DrawRow(ImDrawListPtr drawList, Row row, float x, float y, float width)
    {
        var right = x + width;
        var valueX = right - TextWidth(row.Value);
        var valueColor = row.Severity switch
        {
            Severity.Warn => WarnColor,
            Severity.Alert => AlertTextColor,
            _ => row.Color
        };

        if (row.Severity == Severity.Alert)
        {
            drawList.AddRectFilled(new Vector2(valueX - 3f, y), new Vector2(right + 3f, y + _rowHeight - 2f), AlertColor);
        }

        drawList.AddText(_font, _fontSize, new Vector2(x, y), LabelColor, row.Label);

        if (row.IsBar)
        {
            var barX = x + TextWidth(row.Label) + LabelGap;
            var barRight = MathF.Max(barX, valueX - LabelGap);
            var barY = y + (_rowHeight - BarHeight) * 0.5f;

            drawList.AddRectFilled(new Vector2(barX, barY), new Vector2(barRight, barY + BarHeight), Color(1f, 1f, 1f, 0.08f));

            var fill = barX + (barRight - barX) * row.Fraction;
            if (fill > barX)
            {
                drawList.AddRectFilled(new Vector2(barX, barY), new Vector2(fill, barY + BarHeight), BarColor(row.Fraction));
            }
        }

        drawList.AddText(_font, _fontSize, new Vector2(valueX, y), valueColor, row.Value);
    }

    private float TextWidth(string text) => _font.CalcTextSizeA(_fontSize, float.MaxValue, 0f, text).X;

    private Cell BeginCell()
    {
        if (_cellCount == _cells.Count)
        {
            _cells.Add(new Cell());
        }

        var cell = _cells[_cellCount++];
        cell.Rows.Clear();
        return cell;
    }

    private static void AddRow(Cell cell, string label, string value, Severity severity = Severity.None)
        => cell.Rows.Add(new Row(label, value, ValueColor, severity, -1f));

    private static void AddRow(Cell cell, string label, string value, uint color)
        => cell.Rows.Add(new Row(label, value, color, Severity.None, -1f));

    private static void AddBar(Cell cell, string label, float fraction, string value)
        => cell.Rows.Add(new Row(label, value, ValueColor, Severity.None, Math.Clamp(fraction, 0f, 1f)));

    private static Severity Pressure(float ratio) => ratio switch
    {
        > 0.9f => Severity.Alert,
        > 0.75f => Severity.Warn,
        _ => Severity.None
    };

    private static uint BarColor(float ratio) => ratio switch
    {
        > 0.9f => Color(0.88f, 0.35f, 0.32f),
        > 0.75f => Color(0.95f, 0.72f, 0.28f),
        _ => Color(0.36f, 0.76f, 0.52f)
    };

    private static uint Color(float r, float g, float b, float a = 1f) => (uint) (a * 255f) << 24 | (uint) (b * 255f) << 16 | (uint) (g * 255f) << 8 | (uint) (r * 255f);
}
