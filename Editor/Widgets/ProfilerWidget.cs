using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Extensions;

namespace Editor.Widgets;

public class ProfilerWidget
{
    private readonly Stack<(string Name, IMemoryDetailsProvider Provider)> _navStack = new();
    private string _selectedLeafNode = string.Empty;

    private class BufferStatDefinition(string label, string value, string? extraValue = null, Vector4? color = null, float minWidth = 0)
    {
        public readonly ImGuiMeasuredText Label = new(label);
        public readonly ImGuiMeasuredText Value = new(value);
        public readonly ImGuiMeasuredText LongValue = new(extraValue != null ? value + extraValue : value);
        public readonly Vector4? Color = color;
        public readonly float MinWidth = minWidth;

        public bool UseLongVersion { get; internal set; }
    }

    private readonly struct ImGuiMeasuredText(string text)
    {
        public readonly string Text = text;
        public readonly float Width = ImGui.CalcTextSize(text).X;
    }

    public void DrawMemoryTable(IMemoryDetailsProvider provider)
    {
        ImGui.BeginDisabled(_navStack.Count == 0);
        if (ImGui.Button("\uf104"))
        {
            _navStack.Pop();
            _selectedLeafNode = string.Empty;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Path:");
        foreach (var (name, _) in _navStack.Reverse())
        {
            ImGui.SameLine();
            ImGui.TextDisabled("/");
            ImGui.SameLine();
            ImGui.Text(name);
        }

        ImGui.Separator();

        var current = provider;
        if (_navStack.Count > 0)
        {
            current = _navStack.Peek().Provider;
        }

        var details = current.GetMemoryDetails().ToList();
        if (details.Count == 0)
        {
            ImGui.TextDisabled("No resources to display");
            return;
        }

        foreach (var detail in details)
        {
            DrawMemoryItem(detail);
        }
    }

    private void DrawMemoryItem(MemoryDetail detail)
    {
        const float itemHeight = 24f;
        var cursorPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var availWidth = ImGui.GetContentRegionAvail().X;

        var nodeId = $"{detail.Name}##{detail.Type}";
        var isSelected = _selectedLeafNode == nodeId;

        if (ImGui.InvisibleButton($"item_{nodeId}", new Vector2(availWidth, itemHeight)))
        {
            if (detail.Provider is IMemoryDetailsProvider provider)
            {
                _navStack.Push((detail.Name, provider));
                _selectedLeafNode = string.Empty;
            }
            else if (detail.Provider != null)
            {
                _selectedLeafNode = isSelected ? string.Empty : nodeId;
            }
        }

        if (ImGui.IsItemHovered() || isSelected)
        {
            drawList.AddRectFilled(
                cursorPos, new Vector2(cursorPos.X + availWidth, cursorPos.Y + itemHeight),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, isSelected ? 0.1f : 0.05f)),
                2f);
        }

        const float padding = 8f;
        const float usageBarWidth = 100f;
        const float usageBarHeight = 10f;
        const float percentageWidth = 45f;
        const float memoryTextWidth = 150f;

        var extraX = 0f;
        var notLeaf = detail.Provider is IMemoryDetailsProvider;
        var hasStats = detail.Provider?.GetBufferStatistics() != null;
        if (notLeaf || hasStats)
        {
            extraX = padding * 1.5f;

            const float radius = 2.5f;
            var dotX = cursorPos.X + padding + 3f;
            var dotY = cursorPos.Y + itemHeight / 2f;
            drawList.AddCircleFilled(new Vector2(dotX, dotY), radius, GenerateDistinctColor(notLeaf ? 3 : hasStats ? 7 : 0, 10));
        }

        var textY = cursorPos.Y + (itemHeight - ImGui.GetTextLineHeight()) / 2f;
        drawList.AddText(new Vector2(cursorPos.X + padding + extraX, textY), ImGui.GetColorU32(ImGuiCol.Text), detail.Name);

        var memoryX = cursorPos.X + availWidth - usageBarWidth - percentageWidth - memoryTextWidth - padding * 3;
        drawList.AddText(
            new Vector2(memoryX, textY),
            ImGui.GetColorU32(new Vector4(0.55f, 0.55f, 0.55f, 1)),
            $"{detail.Used.GetReadableSize()} / {detail.Allocated.GetReadableSize()}"
        );

        var percentage = detail.UsagePercentage;
        var usageColor = percentage < 50 ? new Vector4(0.85f, 0.45f, 0.45f, 1) :
            percentage < 70 ? new Vector4(0.85f, 0.7f, 0.4f, 1) :
            percentage < 85 ? new Vector4(0.5f, 0.75f, 0.5f, 1) :
            new Vector4(0.4f, 0.6f, 0.8f, 1);

        var barX = cursorPos.X + availWidth - usageBarWidth - percentageWidth - padding * 2;
        var barY = cursorPos.Y + (itemHeight - usageBarHeight) / 2f;
        drawList.AddRectFilled(
            new Vector2(barX, barY),
            new Vector2(barX + usageBarWidth, barY + usageBarHeight),
            ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 0.5f)),
            2f
        );

        var fillWidth = (float)(usageBarWidth * (percentage / 100f));
        if (fillWidth > 0)
        {
            drawList.AddRectFilled(
                new Vector2(barX, barY),
                new Vector2(barX + fillWidth, barY + usageBarHeight),
                ImGui.GetColorU32(usageColor),
                2f
            );
        }

        var percentX = cursorPos.X + availWidth - percentageWidth;
        drawList.AddText(
            new Vector2(percentX, textY),
            ImGui.GetColorU32(new Vector4(0.7f, 0.7f, 0.7f, 1)),
            $"{percentage:F0}%"
        );

        if (isSelected && detail.Provider?.GetBufferStatistics() is { } stats)
        {
            DrawExpandedBufferStats(stats);
        }
    }

    private void DrawExpandedBufferStats(BufferStatistics stats)
    {
        var definitions = new[]
        {
            new BufferStatDefinition("Capacity", $"{stats.Capacity:N0}"),
            new BufferStatDefinition("Used", $"{stats.UsedItems:N0}", $" ({(float)stats.UsedItems / stats.Capacity * 100:F1}%)", minWidth: 400f),
            new BufferStatDefinition("Free", $"{stats.FreeItems:N0}", $" ({(float)stats.FreeItems / stats.Capacity * 100:F1}%)"),
            new BufferStatDefinition("Frag", $"{stats.FragmentationPercentage:F1}%", color:
                stats.FragmentationPercentage < 20 ? new Vector4(0.5f, 0.75f, 0.65f, 1) :
                stats.FragmentationPercentage < 50 ? new Vector4(0.8f, 0.65f, 0.4f, 1) :
                new Vector4(0.7f, 0.4f, 0.4f, 1),
                minWidth: 150f
            )
        };

        DrawStatsPanel(definitions);
        DrawLargeBufferVisualization(stats);
    }

    private void DrawLargeBufferVisualization(BufferStatistics stats)
    {
        if (stats.Capacity == 0) return;

        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var pixelsPerItem = availWidth / stats.Capacity;

        const float height = 100f;
        drawList.AddRectFilled(cursorPos, new Vector2(cursorPos.X + availWidth, cursorPos.Y + height), ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1)));

        var length = stats.Allocations.Count;
        for (var i = 0; i < length; i++)
        {
            var alloc = stats.Allocations[i];
            var startX = cursorPos.X + alloc.StartIndex * pixelsPerItem;
            var endX = cursorPos.X + (alloc.EndIndex + 1) * pixelsPerItem;

            drawList.AddRectFilled(cursorPos with { X = startX }, new Vector2(endX, cursorPos.Y + height), GenerateDistinctColor(i, length));
        }

        foreach (var block in stats.FreeBlocks)
        {
            var startX = cursorPos.X + block.StartIndex * pixelsPerItem;
            var endX = cursorPos.X + (block.StartIndex + block.Length) * pixelsPerItem;
            var blockWidth = endX - startX;

            const float stripeSpacing = 8f;
            var numStripes = (int)Math.Ceiling((blockWidth + height) / stripeSpacing);
            for (var i = 0; i < numStripes; i++)
            {
                var offset = i * stripeSpacing;

                var lineStartX = startX + offset;
                var lineStartY = cursorPos.Y;
                var lineEndX = startX + offset - height;
                var lineEndY = cursorPos.Y + height;

                if (lineStartX > endX)
                {
                    var excess = lineStartX - endX;
                    lineStartX = endX;
                    lineStartY = cursorPos.Y + excess;
                }

                if (lineEndX < startX)
                {
                    var excess = startX - lineEndX;
                    lineEndX = startX;
                    lineEndY = cursorPos.Y + height - excess;
                }

                lineEndY -= 1;
                if (lineStartY <= cursorPos.Y + height && lineEndY >= cursorPos.Y)
                {
                    drawList.AddLine(
                        new Vector2(lineStartX, lineStartY),
                        new Vector2(lineEndX, lineEndY),
                        ImGui.GetColorU32(new Vector4(0.35f, 0.35f, 0.35f, 0.7f)),
                        2f
                    );
                }
            }
        }

        drawList.AddRect(
            cursorPos,
            new Vector2(cursorPos.X + availWidth, cursorPos.Y + height),
            ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.25f, 1)),
            0f,
            ImDrawFlags.None,
            1f
        );

        ImGui.InvisibleButton("BufferVisLarge", new Vector2(availWidth, height));
        if (ImGui.IsItemHovered())
        {
            var mousePos = ImGui.GetMousePos();
            var relativeX = mousePos.X - cursorPos.X;
            var index = (int)(relativeX / pixelsPerItem);

            if (index >= 0 && index < stats.Capacity)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"Index: {index}");

                var foundAlloc = stats.Allocations.FirstOrDefault(a => index >= a.StartIndex && index <= a.EndIndex);
                if (foundAlloc != null)
                {
                    ImGui.Separator();
                    ImGui.TextUnformatted($"Allocation ID: {foundAlloc.AllocationId}");
                    ImGui.TextUnformatted($"Range: [{foundAlloc.StartIndex}..{foundAlloc.EndIndex}]");
                    ImGui.TextUnformatted($"Length: {foundAlloc.Length}");
                    ImGui.TextUnformatted($"Created: {FormatTimeAgo(foundAlloc.CreatedAt)}");
                    if (foundAlloc.LastModified.HasValue)
                    {
                        ImGui.TextUnformatted($"Modified: {FormatTimeAgo(foundAlloc.LastModified.Value)}");
                    }
                }
                else
                {
                    var foundFree = stats.FreeBlocks.FirstOrDefault(fb => index >= fb.StartIndex && index < fb.StartIndex + fb.Length);
                    if (foundFree.StartIndex != 0 || foundFree.Length != 0)
                    {
                        ImGui.Separator();
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Free Block");
                        ImGui.TextUnformatted($"Range: [{foundFree.StartIndex}..{foundFree.StartIndex + foundFree.Length - 1}]");
                        ImGui.TextUnformatted($"Length: {foundFree.Length}");
                    }
                    else
                    {
                        ImGui.Separator();
                        ImGui.TextDisabled("Unused space");
                    }
                }

                ImGui.EndTooltip();
            }
        }

        ImGui.Spacing();
    }

    public void DrawMemorySummary(IMemorySizeProvider provider)
    {
        var wastedPercentage = (float)provider.Wasted / provider.Allocated * 100;
        var wastedColor = wastedPercentage > 30 ? new Vector4(0.85f, 0.45f, 0.45f, 1) :
                         wastedPercentage > 15 ? new Vector4(0.85f, 0.7f, 0.4f, 1) :
                         new Vector4(0.5f, 0.75f, 0.65f, 1);

        var definitions = new[]
        {
            new BufferStatDefinition("Used", provider.Used.GetReadableSize()),
            new BufferStatDefinition("Allocated", provider.Allocated.GetReadableSize()),
            new BufferStatDefinition("Wasted", provider.Wasted.GetReadableSize(), $" ({wastedPercentage:F1}%)", color: wastedColor)
        };

        DrawStatsPanel(definitions);
    }

    private void DrawStatsPanel(BufferStatDefinition[] definitions)
    {
        const float padding = 12f;
        const float panelHeight = 35f;
        const float separatorWidth = 1f;

        var cursorPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var availWidth = ImGui.GetContentRegionAvail().X;

        drawList.AddRectFilled(
            cursorPos,
            new Vector2(cursorPos.X + availWidth, cursorPos.Y + panelHeight),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 0.5f)),
            4f
        );

        definitions = definitions.Where(s => s.MinWidth <= availWidth).ToArray();
        if (definitions.Length > 0)
        {
            var totalContentWithLong = padding * 2 + definitions.Sum(entry => entry.Label.Width + padding + entry.LongValue.Width);
            if (totalContentWithLong <= availWidth)
            {
                foreach (var entry in definitions)
                {
                    entry.UseLongVersion = true;
                }
            }

            var numSeparators = definitions.Length - 1;
            var totalContentWidth = padding * 2 + definitions.Sum(d => d.Label.Width + padding + (d.UseLongVersion ? d.LongValue.Width : d.Value.Width));
            var remainingSpace = availWidth - totalContentWidth;
            var gapBetweenSections = numSeparators > 0 ? Math.Max(15f, remainingSpace / numSeparators) : 0f;

            var textY = cursorPos.Y + 9f;
            var textX = cursorPos.X + padding;

            for (var i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];

                drawList.AddText(new Vector2(textX, textY), ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1)), def.Label.Text);
                textX += def.Label.Width + padding;

                var value = def.UseLongVersion ? def.LongValue : def.Value;
                var valueColor = def.Color.HasValue ? ImGui.GetColorU32(def.Color.Value) : ImGui.GetColorU32(ImGuiCol.Text);
                drawList.AddText(new Vector2(textX, textY), valueColor, value.Text);
                textX += value.Width;

                if (i < numSeparators)
                {
                    textX += gapBetweenSections;

                    drawList.AddLine(
                        new Vector2(textX - gapBetweenSections / 2, cursorPos.Y + 8),
                        new Vector2(textX - gapBetweenSections / 2, cursorPos.Y + panelHeight - 8),
                        ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1)),
                        separatorWidth
                    );
                }
            }
        }

        ImGui.Dummy(new Vector2(0, panelHeight));
    }

    public void DrawPerformanceMetrics(SystemProfiler profiler, string? idSuffix = null)
    {
        var colors = new Dictionary<ProfilerMetric, Vector4>
        {
            { ProfilerMetric.CpuRender, new Vector4(0.2f, 0.8f, 0.2f, 1.0f) },
            { ProfilerMetric.GpuRender, new Vector4(1.0f, 0.2f, 0.2f, 1.0f) },
            { ProfilerMetric.Update, new Vector4(0.2f, 0.6f, 1.0f, 1.0f) },
            { ProfilerMetric.Load, new Vector4(1.0f, 0.8f, 0.2f, 1.0f) },
            { ProfilerMetric.Custom, new Vector4(1.0f, 0.4f, 0.8f, 1.0f) }
        };
        var allMetrics = profiler.GetAllMetrics();

        if (ImGui.BeginTable($"##MetricsTable{idSuffix}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthStretch, 2.0f);
            ImGui.TableSetupColumn("Last", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Avg", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Max", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableHeadersRow();

            foreach (var (metric, data) in allMetrics)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if (colors.TryGetValue(metric, out var color))
                {
                    if (metric != ProfilerMetric.Load)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.BulletText($"{metric.GetDescription()}");
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        ImGui.BulletText(metric.ToString());
                    }
                }
                else
                {
                    ImGui.BulletText(metric.ToString());
                }

                ImGui.TableNextColumn();
                ImGui.Text($"{data.LastTimeElapsedMs:F2} ms");
                ImGui.TableNextColumn();
                if (metric != ProfilerMetric.Load)
                {
                    ImGui.Text($"{data.AverageTimeElapsedMs:F2} ms");
                }
                else
                {
                    ImGui.TextDisabled("-");
                }

                ImGui.TableNextColumn();
                if (metric != ProfilerMetric.Load)
                {
                    ImGui.Text($"{data.MaxTimeElapsedMs:F2} ms");
                }
                else
                {
                    ImGui.TextDisabled("-");
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();

        if (allMetrics.Where(m => m.Key != ProfilerMetric.Load).ToArray() is { Length: > 0 } plottable)
        {
            var allTimeMax = plottable.Max(m => m.Value.AllTimeMaxTimeElapsedMs);
            var recentMax = plottable.Max(m => m.Value.MaxTimeElapsedMs);
            var globalMax = recentMax >= allTimeMax * 0.5f ? allTimeMax : Math.Max(recentMax * 2f, 0.1f);

            var minRecentMax = plottable.Min(m => m.Value.MaxTimeElapsedMs);
            if (minRecentMax > 0 && minRecentMax < globalMax * 0.1f)
            {
                globalMax = Math.Max(recentMax * 1.2f, 0.1f);
            }

            if (ImGui.BeginChild($"##PlotChild{idSuffix}", new Vector2(-1, 80), ImGuiChildFlags.Borders))
            {
                var drawList = ImGui.GetWindowDrawList();
                var plotMin = ImGui.GetCursorScreenPos();
                var plotMax = plotMin + ImGui.GetContentRegionAvail();
                var plotWidth = plotMax.X - plotMin.X;
                var plotHeight = plotMax.Y - plotMin.Y;

                drawList.AddRectFilled(plotMin, plotMax, ImGui.GetColorU32(ImGuiCol.FrameBg));

                var gridColor = ImGui.GetColorU32(ImGuiCol.Border);
                for (var i = 1; i < 4; i++)
                {
                    var y = plotMin.Y + plotHeight * i / 4;
                    drawList.AddLine(plotMin with { Y = y }, plotMax with { Y = y }, gridColor, 0.5f);
                }

                foreach (var (metric, data) in plottable)
                {
                    if (!colors.TryGetValue(metric, out var color))
                        color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

                    var colorU32 = ImGui.GetColorU32(color);
                    var timeData = data.TimeElapsedMs;
                    for (var i = 0; i < SystemProfiler.MaxFrameHistory - 1; i++)
                    {
                        var x1 = plotMin.X + (plotWidth * (SystemProfiler.MaxFrameHistory - 1 - i)) / SystemProfiler.MaxFrameHistory;
                        var x2 = plotMin.X + (plotWidth * (SystemProfiler.MaxFrameHistory - 2 - i)) / SystemProfiler.MaxFrameHistory;

                        var t1 = globalMax > 0 ? Math.Clamp(timeData[i] / globalMax, 0f, 1f) : 0f;
                        var t2 = globalMax > 0 ? Math.Clamp(timeData[i + 1] / globalMax, 0f, 1f) : 0f;

                        var y1 = plotMax.Y - (plotHeight * t1);
                        var y2 = plotMax.Y - (plotHeight * t2);

                        drawList.AddLine(new Vector2(x1, y1), new Vector2(x2, y2), colorU32, 1.5f);
                    }
                }

                drawList.AddRect(plotMin, plotMax, ImGui.GetColorU32(ImGuiCol.Border));

                var labelText = allTimeMax > recentMax * 1.5f
                    ? $"{globalMax:F2} ms (peak: {allTimeMax:F2} ms)"
                    : $"{globalMax:F2} ms";
                drawList.AddText(new Vector2(plotMin.X + 4, plotMin.Y + 2), ImGui.GetColorU32(ImGuiCol.Text), labelText);
            }

            ImGui.EndChild();
        }
    }

    private string FormatTimeAgo(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - timestamp.ToUniversalTime();

        if (elapsed.TotalSeconds < 60)
            return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}min ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";

        return timestamp.ToLocalTime().ToString("MMM dd");
    }

    private uint GenerateDistinctColor(int index, int total)
    {
        var hue = (float)index / total;
        return ImGui.GetColorU32(HsvToRgb(hue, 0.7f, 0.9f));
    }

    private Vector4 HsvToRgb(float h, float s, float v)
    {
        var c = v * s;
        var x = c * (1 - MathF.Abs(h * 6 % 2 - 1));
        var m = v - c;

        float r, g, b;
        switch (h)
        {
            case < 1f / 6f:
                r = c; g = x; b = 0;
                break;
            case < 2f / 6f:
                r = x; g = c; b = 0;
                break;
            case < 3f / 6f:
                r = 0; g = c; b = x;
                break;
            case < 4f / 6f:
                r = 0; g = x; b = c;
                break;
            case < 5f / 6f:
                r = x; g = 0; b = c;
                break;
            default:
                r = c; g = 0; b = x;
                break;
        }

        return new Vector4(r + m, g + m, b + m, 1);
    }
}
