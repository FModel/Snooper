using System.Numerics;
using ImGuiNET;
using Snooper.Core;

namespace Editor.Widgets;

public class ProfilerOverlayWidget
{
    private const float Margin = 8f;          // gap from the viewport edges
    private const float Inner = 8f;           // padding inside the panel
    private const float ToolbarClearance = 38f;
    private const float BottomClearance = 34f; // clears the FPS readout
    private const float PanelWidth = 320f;
    private const float GraphHeight = 46f;
    private const float LabelHeight = 16f;
    private const float GraphGap = 6f;
    private const float RowHeight = 18f;
    private const float BreadcrumbHeight = 18f;
    private const float HeaderPad = 6f; // gap between the breadcrumb's divider and the graphs below

    // Path from the profiler root down to the currently visualized node, by zone name
    // (e.g. ["Frame"], ["Frame", "Deferred Pass"], ["Frame", "Deferred Pass", "StaticMeshRenderSystem"]).
    private readonly List<string> _path = ["Frame"];

    private static readonly uint[] _palette =
    [
        Color(0.90f, 0.32f, 0.28f), Color(0.36f, 0.72f, 0.36f), Color(0.30f, 0.55f, 0.95f),
        Color(0.95f, 0.75f, 0.25f), Color(0.70f, 0.45f, 0.90f), Color(0.30f, 0.80f, 0.80f),
        Color(0.95f, 0.55f, 0.30f), Color(0.55f, 0.85f, 0.35f), Color(0.90f, 0.45f, 0.70f),
        Color(0.50f, 0.60f, 0.70f), Color(0.80f, 0.80f, 0.40f), Color(0.40f, 0.70f, 0.55f),
    ];

    public void Draw(ImDrawListPtr drawList, Vector2 contentPos, Vector2 contentSize)
    {
        if (!Profiler.Enabled) return;

        var maxHeight = contentSize.Y - ToolbarClearance - BottomClearance;
        if (maxHeight < GraphHeight * 2f || contentSize.X < PanelWidth + Margin * 2f) return;

        var origin = contentPos + new Vector2(Margin, ToolbarClearance);
        var savedCursor = ImGui.GetCursorScreenPos();

        Profiler.Read(root =>
        {
            if (root.Children.Count == 0)
            {
                var emptySize = new Vector2(PanelWidth, Inner * 2f + RowHeight);
                drawList.AddRectFilled(origin, origin + emptySize, Color(0.06f, 0.06f, 0.08f, 0.72f));
                drawList.AddRect(origin, origin + emptySize, Color(1f, 1f, 1f, 0.10f));
                drawList.AddText(origin + new Vector2(Inner, Inner), Color(0.7f, 0.7f, 0.75f), "No profiler data yet.");
                return;
            }

            var node = ResolveNode(root);
            var series = node.Children.Count > 0 ? node.Children : (IReadOnlyList<ProfilerNode>)[node];

            // Size the panel to its content and stop there, but never past the available space.
            const float headerHeight = Inner * 2f + BreadcrumbHeight;
            const float graphsBlock = headerHeight + HeaderPad + (LabelHeight + GraphHeight) + GraphGap + (LabelHeight + GraphHeight);
            const float headerBlock = 8f + 1f + 6f + RowHeight + 2f; // separator + total line
            var legendBlock = (series.Count + (_path.Count > 1 ? 1 : 0)) * RowHeight;
            var panelHeight = MathF.Min(graphsBlock + headerBlock + legendBlock + Inner, maxHeight);
            var panelSize = new Vector2(PanelWidth, panelHeight);

            drawList.AddRectFilled(origin, origin + panelSize, Color(0.06f, 0.06f, 0.08f, 0.72f));
            drawList.AddRect(origin, origin + panelSize, Color(1f, 1f, 1f, 0.10f));

            // Header band: visually separates the breadcrumb (a toolbar) from the graphs below.
            drawList.AddRectFilled(origin, origin + panelSize with { Y = headerHeight }, Color(1f, 1f, 1f, 0.03f));
            drawList.AddLine(origin with { Y = origin.Y + headerHeight }, new Vector2(origin.X + panelSize.X, origin.Y + headerHeight), Color(1f, 1f, 1f, 0.10f));

            ImGui.PushID("ProfilerOverlay");
            drawList.PushClipRect(origin, origin + panelSize, true);
            DrawBreadcrumb(drawList, origin + new Vector2(Inner, Inner), root);
            DrawPanel(drawList, origin, panelSize, node, series);
            drawList.PopClipRect();
            ImGui.PopID();
        });

        ImGui.SetCursorScreenPos(savedCursor);
    }

    private ProfilerNode ResolveNode(ProfilerNode root)
    {
        var node = root;
        for (var i = 0; i < _path.Count; i++)
        {
            ProfilerNode? next = null;
            foreach (var child in node.Children)
            {
                if (child.Name == _path[i])
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
            {
                _path.RemoveRange(i, _path.Count - i);
                break;
            }
            node = next;
        }

        if (node == root)
        {
            node = root.Children[0];
            _path.Clear();
            _path.Add(node.Name);
        }

        return node;
    }

    private void DrawBreadcrumb(ImDrawListPtr drawList, Vector2 pos, ProfilerNode root)
    {
        var x = pos.X;

        foreach (var group in root.Children)
        {
            var active = _path.Count > 0 && _path[0] == group.Name;
            var textSize = ImGui.CalcTextSize(group.Name);
            var size = new Vector2(textSize.X + 10f, BreadcrumbHeight);

            ImGui.SetCursorScreenPos(pos with { X = x });
            var clicked = ImGui.InvisibleButton(group.Name, size);
            var hovered = ImGui.IsItemHovered();

            if (active || hovered)
            {
                drawList.AddRectFilled(pos with { X = x }, new Vector2(x + size.X, pos.Y + BreadcrumbHeight), Color(1f, 1f, 1f, active ? 0.12f : 0.06f));
            }

            var textColor = active ? Color(1f, 1f, 1f) : hovered ? Color(0.85f, 0.85f, 0.9f) : Color(0.6f, 0.6f, 0.65f);
            drawList.AddText(new Vector2(x + 5f, pos.Y + 2f), textColor, group.Name);

            if (clicked)
            {
                _path.Clear();
                _path.Add(group.Name);
            }

            x += size.X + 4f;
        }
    }

    private void DrawPanel(ImDrawListPtr drawList, Vector2 origin, Vector2 panelSize, ProfilerNode node, IReadOnlyList<ProfilerNode> series)
    {
        var graphSize = new Vector2(PanelWidth - Inner * 2f, GraphHeight);
        var x0 = origin.X + Inner;

        var cpuGraphPos = new Vector2(x0, origin.Y + Inner * 2f + BreadcrumbHeight + HeaderPad + LabelHeight);
        var gpuGraphPos = cpuGraphPos + new Vector2(0f, GraphHeight + LabelHeight + GraphGap);

        const int selected = 0;
        DrawGraph(drawList, cpuGraphPos, graphSize, series, false, selected, "CPU", node.Cpu);
        DrawGraph(drawList, gpuGraphPos, graphSize, series, true, selected, "GPU", node.Gpu);

        var y = gpuGraphPos.Y + GraphHeight + 8f;
        drawList.AddLine(new Vector2(x0, y), new Vector2(origin.X + PanelWidth - Inner, y), Color(1f, 1f, 1f, 0.12f));
        y += 6f;

        drawList.AddText(new Vector2(x0, y), Color(0.7f, 0.7f, 0.75f),
            $"{node.Name}  {node.Cpu.TimeElapsedMs[selected]:F2} / {node.Gpu.TimeElapsedMs[selected]:F2} ms   avg {node.Cpu.AverageTimeElapsedMs:F2} / {node.Gpu.AverageTimeElapsedMs:F2}");
        y += RowHeight + 2f;

        DrawLegend(drawList, x0, y, origin.Y + panelSize.Y - Inner, series, selected);
    }

    private void DrawGraph(ImDrawListPtr drawList, Vector2 pos, Vector2 size, IReadOnlyList<ProfilerNode> series, bool gpu, int selected, string label, ProfilerMetricData total)
    {
        const int history = ProfilerMetricData.MaxFrameHistory;
        var frameW = size.X / history;

        // Vertical scale: the largest total frame time in the visible history (min-clamped).
        var maxTime = 0.1f;
        for (var i = 0; i < history; i++)
        {
            var sum = 0f;
            foreach (var task in series)
                sum += Series(task, gpu).TimeElapsedMs[i];
            if (sum > maxTime) maxTime = sum;
        }

        drawList.AddText(pos - new Vector2(0f, LabelHeight), Color(0.8f, 0.8f, 0.85f), $"{label}  {total.TimeElapsedMs[selected]:F2} ms");

        drawList.AddRectFilled(pos, pos + size, Color(0f, 0f, 0f, 0.35f));

        for (var i = 0; i < history; i++)
        {
            var bx = pos.X + size.X - (i + 1) * frameW;
            var yBottom = pos.Y + size.Y;

            for (var t = 0; t < series.Count; t++)
            {
                var ms = Series(series[t], gpu).TimeElapsedMs[i];
                if (ms <= 0f) continue;

                var h = ms / maxTime * size.Y;
                var yTop = yBottom - h;
                drawList.AddRectFilled(new Vector2(bx, yTop), new Vector2(bx + frameW + 0.5f, yBottom), _palette[t % _palette.Length]);
                yBottom = yTop;
            }
        }

        drawList.AddRect(pos, pos + size, Color(1f, 1f, 1f, 0.15f));
    }

    private void DrawLegend(ImDrawListPtr drawList, float x, float y, float maxY, IReadOnlyList<ProfilerNode> series, int selected)
    {
        const float square = 10f;
        var right = x + PanelWidth - Inner * 2f;

        // fake ".." entry to drill back up the hierarchy, if we're not at the root.
        if (_path.Count > 1 && y + RowHeight <= maxY)
        {
            var accentColor = Color(0.30f, 0.55f, 0.95f);

            ImGui.SetCursorScreenPos(new Vector2(x, y));
            if (ImGui.InvisibleButton("legendUp", new Vector2(right - x, RowHeight)))
                _path.RemoveAt(_path.Count - 1);
            var hoveredUp = ImGui.IsItemHovered();

            drawList.AddRectFilled(new Vector2(x - 2f, y), new Vector2(right + 2f, y + RowHeight), Color(0.30f, 0.55f, 0.95f, hoveredUp ? 0.22f : 0.12f));
            drawList.AddRectFilled(new Vector2(x - 2f, y), new Vector2(x, y + RowHeight), accentColor);

            var upColor = hoveredUp ? Color(1f, 1f, 1f) : Color(0.85f, 0.88f, 0.95f);
            drawList.AddText(new Vector2(x + square + 6f, y + 1f), upColor, "..");

            y += RowHeight;
        }

        for (var t = 0; t < series.Count && y + RowHeight <= maxY; t++)
        {
            var task = series[t];
            var drillable = task.Children.Count > 0;

            // Whole-row hit target so a click drills into the zone's sub-timings.
            if (drillable)
            {
                ImGui.SetCursorScreenPos(new Vector2(x, y));
                if (ImGui.InvisibleButton($"legend{t}", new Vector2(right - x, RowHeight)))
                    _path.Add(task.Name);
                if (ImGui.IsItemHovered())
                    drawList.AddRectFilled(new Vector2(x - 2f, y), new Vector2(right + 2f, y + RowHeight), Color(1f, 1f, 1f, 0.06f));
            }

            var cpuMs = task.Cpu.TimeElapsedMs[selected];
            var gpuMs = task.HasGpu ? task.Gpu.TimeElapsedMs[selected] : 0f;

            var sqTop = new Vector2(x, y + (RowHeight - square) / 2f);
            drawList.AddRectFilled(sqTop, sqTop + new Vector2(square, square), _palette[t % _palette.Length]);

            drawList.AddText(new Vector2(x + square + 6f, y + 1f), Color(0.85f, 0.85f, 0.88f), task.Name);
            if (drillable)
                drawList.AddText(new Vector2(x + square + 6f + ImGui.CalcTextSize(task.Name).X + 3f, y + 1f), Color(0.5f, 0.5f, 0.55f), "\uf105");

            var timing = task.HasGpu ? $"{cpuMs:F2} / {gpuMs:F2} ms" : $"{cpuMs:F2} ms";
            var timingWidth = ImGui.CalcTextSize(timing).X;
            drawList.AddText(new Vector2(right - timingWidth, y + 1f), Color(0.62f, 0.62f, 0.68f), timing);

            y += RowHeight;
        }
    }

    private static ProfilerMetricData Series(ProfilerNode node, bool gpu) => gpu ? node.Gpu : node.Cpu;

    private static uint Color(float r, float g, float b, float a = 1f) => (uint)(a * 255f) << 24 | (uint)(b * 255f) << 16 | (uint)(g * 255f) << 8 | (uint)(r * 255f);
}
