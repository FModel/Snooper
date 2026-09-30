using System.Numerics;
using System.Runtime.InteropServices;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Extensions;
using Snooper.UI;

namespace Editor.Widgets.Cache;

public class BufferTab : ICacheTab
{
    public string Title => "Buffers";

    private const float RefreshInterval = 0.25f; // every figure is a formatted string, and the statistics of a buffer are a copy of its allocation list

    private readonly record struct Row(string Name, string Value, float Fraction, bool Drillable, bool Selectable);

    // names from the root down, resolved on every refresh so a scene reload cannot leave a disposed provider on screen
    private readonly List<string> _path = [];
    private string? _selected;

    private readonly List<MemoryDetail> _details = [];
    private readonly List<Row> _rows = [];
    private readonly List<MemoryChart.Segment> _segments = [];
    private readonly MemoryChart.Tile[] _totals = new MemoryChart.Tile[3];

    private BufferStatistics? _statistics;
    private readonly MemoryChart.Tile[] _figures = new MemoryChart.Tile[4];
    private int[] _owners = [];
    private float[] _largest = [];
    private float[] _covered = [];
    private bool _mapDirty;

    private float _sinceRefresh = float.MaxValue;

    public void Draw(EditorManager editor)
    {
        _sinceRefresh += ImGui.GetIO().DeltaTime;
        if (_sinceRefresh >= RefreshInterval)
        {
            _sinceRefresh = 0f;
            Refresh(editor);
        }

        var gap = new Vector2(0f, MemoryChart.Gap);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });

        MemoryChart.Tiles(_totals);
        ImGui.Dummy(gap);
        MemoryChart.Bar(CollectionsMarshal.AsSpan(_segments));
        ImGui.Dummy(gap);
        DrawPath();
        ImGui.Dummy(gap);

        // the selected buffer sits at the bottom of the panel, the list takes what is left and scrolls
        var hasMap = _statistics is { Capacity: > 0 };
        var detailsHeight = 0f;
        if (_selected != null)
        {
            detailsHeight = gap.Y + MemoryChart.Unit + gap.Y + (hasMap ? MemoryChart.TilesHeight + gap.Y + MapHeight : MemoryChart.Unit);
        }

        if (ImGui.BeginChild("##BufferList", new Vector2(0f, MathF.Max(MemoryChart.RowHeight * 2f, ImGui.GetContentRegionAvail().Y - detailsHeight))))
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];

                ImGui.PushID(i);
                var clicked = MemoryChart.Row(_segments[i].Color, row.Name, row.Value, row.Fraction, row.Drillable, row.Name == _selected, row.Drillable || row.Selectable);
                ImGui.PopID();

                if (!clicked) continue;

                if (row.Drillable)
                {
                    _path.Add(row.Name);
                    _selected = null;
                }
                else
                {
                    _selected = row.Name == _selected ? null : row.Name;
                }
                _sinceRefresh = float.MaxValue;
            }
        }
        ImGui.EndChild();

        if (_selected is { } selected && detailsHeight > 0f)
        {
            ImGui.Dummy(gap);
            ImGui.SeparatorText(selected);
            ImGui.Dummy(gap);

            if (_statistics is { Capacity: > 0 } statistics)
            {
                MemoryChart.Tiles(_figures);
                ImGui.Dummy(gap);
                DrawMap(statistics);
            }
            else
            {
                ImGui.TextDisabled("This entry keeps no allocation list.");
            }
        }

        ImGui.PopStyleVar();
    }

    private static float MapHeight => MemoryChart.Unit * 2.5f;

    private void DrawPath()
    {
        if (MemoryChart.Chip("Scene", _path.Count == 0)) Truncate(0);

        for (var i = 0; i < _path.Count; i++)
        {
            ImGui.SameLine(0f, 0.2f * MemoryChart.Unit);
            if (MemoryChart.Chip(_path[i], i == _path.Count - 1)) Truncate(i + 1);
        }

        void Truncate(int count)
        {
            if (count >= _path.Count) return;

            _path.RemoveRange(count, _path.Count - count);
            _selected = null;
            _sinceRefresh = float.MaxValue;
        }
    }

    private void Refresh(EditorManager editor)
    {
        IMemoryDetailsProvider current = editor;
        for (var i = 0; i < _path.Count; i++)
        {
            IMemoryDetailsProvider? next = null;
            foreach (var detail in current.GetMemoryDetails())
            {
                if (detail.Name != _path[i] || detail.Provider is not IMemoryDetailsProvider provider) continue;

                next = provider;
                break;
            }

            if (next == null)
            {
                _path.RemoveRange(i, _path.Count - i);
                _selected = null;
                break;
            }
            current = next;
        }

        _details.Clear();
        _details.AddRange(current.GetMemoryDetails());

        long used = 0;
        long allocated = 0;
        foreach (var detail in _details)
        {
            used += detail.Used;
            allocated += detail.Allocated;
        }

        _rows.Clear();
        _segments.Clear();
        IBufferStatisticsProvider? selected = null;
        for (var i = 0; i < _details.Count; i++)
        {
            var detail = _details[i];
            var drillable = detail.Provider is IMemoryDetailsProvider;

            _rows.Add(new Row(
                detail.Name,
                $"{detail.Used.GetReadableSize()} / {detail.Allocated.GetReadableSize()}",
                detail.Allocated > 0 ? (float) detail.Used / detail.Allocated : 0f,
                drillable,
                !drillable && detail.Provider != null));

            // what is used, side by side, so the bar reads as one block followed by what is allocated for nothing
            _segments.Add(new MemoryChart.Segment(detail.Name, ProfilerOverlayWidget._palette[i % ProfilerOverlayWidget._palette.Length], allocated > 0 ? (float) detail.Used / allocated : 0f));

            if (detail.Name == _selected) selected = detail.Provider;
        }

        var unused = allocated - used;
        _totals[0] = new MemoryChart.Tile("Used", used.GetReadableSize());
        _totals[1] = new MemoryChart.Tile("Allocated", allocated.GetReadableSize());
        _totals[2] = new MemoryChart.Tile("Unused", allocated > 0 ? $"{unused.GetReadableSize()} ({(float) unused / allocated * 100f:F1}%)" : unused.GetReadableSize());

        if (selected == null) _selected = null;
        _statistics = selected?.GetBufferStatistics();
        _mapDirty = true;

        if (_statistics is not { Capacity: > 0 } statistics) return;

        _figures[0] = new MemoryChart.Tile("Capacity", $"{statistics.Capacity:N0}");
        _figures[1] = new MemoryChart.Tile("Used", $"{statistics.UsedItems:N0} ({(float) statistics.UsedItems / statistics.Capacity * 100f:F1}%)");
        _figures[2] = new MemoryChart.Tile("Free Blocks", $"{statistics.FreeBlocks.Count:N0}");
        _figures[3] = new MemoryChart.Tile("Fragmentation", $"{statistics.FragmentationPercentage:F1}%", ImGui.GetColorU32(statistics.FragmentationPercentage switch
        {
            < 20 => Settings.GreenColor,
            < 50 => Settings.OrangeColor,
            _ => Settings.RedColor
        }));
    }

    // one colour per allocation. A pixel column takes the colour of the allocation that fills most of it, and none when
    // it is mostly free, so a buffer of a million items costs at most one rectangle per column
    private void DrawMap(BufferStatistics statistics)
    {
        var origin = ImGui.GetCursorScreenPos();
        var columns = Math.Max(1, (int) ImGui.GetContentRegionAvail().X);
        var size = new Vector2(columns, MapHeight);
        var perColumn = (float) statistics.Capacity / columns;

        if (_mapDirty || _owners.Length != columns)
        {
            _mapDirty = false;
            Rebuild();
        }

        var drawList = ImGui.GetWindowDrawList();
        var palette = ProfilerOverlayWidget._palette;
        drawList.AddRectFilled(origin, origin + size, MemoryChart.Color(0f, 0f, 0f, 0.35f));

        var start = 0;
        while (start < columns)
        {
            var owner = _owners[start];
            var end = start + 1;
            while (end < columns && _owners[end] == owner) end++;

            if (owner >= 0) drawList.AddRectFilled(origin + new Vector2(start, 0f), origin + new Vector2(end, size.Y), palette[owner % palette.Length]);
            start = end;
        }

        drawList.AddRect(origin, origin + size, MemoryChart.Color(1f, 1f, 1f, 0.15f));

        ImGui.InvisibleButton("##BufferMap", size);
        if (ImGui.IsItemHovered()) DrawTooltip();

        void Rebuild()
        {
            if (_owners.Length != columns)
            {
                _owners = new int[columns];
                _largest = new float[columns];
                _covered = new float[columns];
            }

            Array.Fill(_owners, -1);
            Array.Clear(_largest);
            Array.Clear(_covered);

            for (var i = 0; i < statistics.Allocations.Count; i++)
            {
                // the end is exclusive: with fewer items than columns one item spreads over several of them
                var allocation = statistics.Allocations[i];
                var first = Math.Min(columns - 1, (int) (allocation.StartIndex / perColumn));
                var last = Math.Min(columns - 1, (int) MathF.Ceiling((allocation.EndIndex + 1) / perColumn) - 1);
                for (var column = first; column <= last; column++)
                {
                    var overlap = MathF.Min(allocation.EndIndex + 1, (column + 1) * perColumn) - MathF.Max(allocation.StartIndex, column * perColumn);
                    if (overlap <= 0f) continue;

                    _covered[column] += overlap;
                    if (overlap <= _largest[column]) continue;

                    _largest[column] = overlap;
                    _owners[column] = i;
                }
            }

            for (var column = 0; column < columns; column++)
            {
                if (_covered[column] < perColumn * 0.5f) _owners[column] = -1;
            }
        }

        void DrawTooltip()
        {
            var column = Math.Clamp((int) (ImGui.GetMousePos().X - origin.X), 0, columns - 1);
            var index = Math.Min(statistics.Capacity - 1, (int) (column * perColumn));

            BufferAllocationMetadata? found = null;
            foreach (var allocation in statistics.Allocations)
            {
                if (index < allocation.StartIndex || index > allocation.EndIndex) continue;

                found = allocation;
                break;
            }

            var text = $"Index {index:N0}";
            if (found != null)
            {
                text += $"\nAllocation {found.AllocationId}: {found.StartIndex:N0} to {found.EndIndex:N0} ({found.Length:N0} items)\nCreated {Ago(found.CreatedAt)}";
                if (found.LastModified is { } modified) text += $", modified {Ago(modified)}";
            }
            else
            {
                text += "\nFree";
            }

            EditorUI.Tooltip(text);
        }
    }

    private static string Ago(DateTime timestamp)
    {
        var elapsed = DateTime.UtcNow - timestamp.ToUniversalTime();
        return elapsed.TotalSeconds switch
        {
            < 60 => $"{(int) elapsed.TotalSeconds}s ago",
            < 3600 => $"{(int) elapsed.TotalMinutes}min ago",
            < 86400 => $"{(int) elapsed.TotalHours}h ago",
            _ => timestamp.ToLocalTime().ToString("MMM dd")
        };
    }
}
