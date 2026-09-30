using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core;

namespace Editor.Widgets;

public class ProfilerOverlayWidget : IViewportCard
{
    public string Title => "Profiler";
    public bool IsOpen { get => Profiler.Enabled; set => Profiler.Enabled = value; }

    private static float Unit => ImGui.GetFrameHeight();
    private static float Line => ImGui.GetTextLineHeightWithSpacing();
    private static float Gap => 0.3f * Unit;
    private static float GraphHeight => 2.2f * Unit;
    private static float Square => 0.5f * Unit;
    private static Vector2 TextOffset => new(0f, (Line - ImGui.GetFontSize()) * 0.5f);

    // Path from the profiler root down to the currently visualized node, by zone name
    // (e.g. ["Frame"], ["Frame", "Deferred Pass"], ["Frame", "Deferred Pass", "StaticMeshRenderSystem"]).
    private readonly List<string> _path = ["Frame"];

    internal static readonly uint[] _palette =
    [
        Color(0.90f, 0.32f, 0.28f), Color(0.36f, 0.72f, 0.36f), Color(0.30f, 0.55f, 0.95f),
        Color(0.95f, 0.75f, 0.25f), Color(0.70f, 0.45f, 0.90f), Color(0.30f, 0.80f, 0.80f),
        Color(0.95f, 0.55f, 0.30f), Color(0.55f, 0.85f, 0.35f), Color(0.90f, 0.45f, 0.70f),
        Color(0.50f, 0.60f, 0.70f), Color(0.80f, 0.80f, 0.40f), Color(0.40f, 0.70f, 0.55f),
        Color(0.65f, 0.35f, 0.30f), Color(0.40f, 0.90f, 0.70f), Color(0.55f, 0.60f, 0.95f),
        Color(0.85f, 0.65f, 0.45f), Color(0.85f, 0.40f, 0.85f), Color(0.35f, 0.60f, 0.75f),
        Color(0.60f, 0.60f, 0.30f), Color(0.50f, 0.35f, 0.60f), Color(0.95f, 0.60f, 0.60f),
        Color(0.25f, 0.55f, 0.45f), Color(0.70f, 0.70f, 0.80f), Color(0.95f, 0.80f, 0.90f),
    ];

    public void Draw(EditorManager editor)
    {
        var root = Profiler.Root;
        if (root.Children.Count == 0)
        {
            ImGui.TextDisabled("No profiler data yet.");
            return;
        }

        var node = ResolveNode(root);
        var series = node.Children.Count > 0 ? node.Children : (IReadOnlyList<ProfilerNode>)[node];

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var y = origin.Y;

        DrawBreadcrumb(drawList, origin, root);
        y += Line + Gap;

        const int selected = 0;
        var graphSize = new Vector2(width, GraphHeight);

        y += Line;
        DrawGraph(drawList, new Vector2(origin.X, y), graphSize, series, false, selected, "CPU", node.Cpu);
        y += GraphHeight + Gap;

        y += Line;
        DrawGraph(drawList, new Vector2(origin.X, y), graphSize, series, true, selected, "GPU", node.Gpu);
        y += GraphHeight + Gap;

        drawList.AddLine(new Vector2(origin.X, y), new Vector2(right, y), Color(1f, 1f, 1f, 0.12f));
        y += Gap;

        // Spelling the pair out here keeps the total line doubling as the key for the unlabelled "a / b" legend rows.
        drawList.AddText(new Vector2(origin.X, y) + TextOffset, Color(0.85f, 0.85f, 0.88f), node.Name);
        var total = $"CPU {node.Cpu.TimeElapsedMs[selected]:F2} / GPU {node.Gpu.TimeElapsedMs[selected]:F2} ms";
        drawList.AddText(new Vector2(right - ImGui.CalcTextSize(total).X, y) + TextOffset, Color(0.7f, 0.7f, 0.75f), total);
        y += Line;

        y = DrawLegend(drawList, new Vector2(origin.X, y), width, series, selected);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y));
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
        var padding = 0.25f * Unit;

        foreach (var group in root.Children)
        {
            var active = _path.Count > 0 && _path[0] == group.Name;
            var textSize = ImGui.CalcTextSize(group.Name);
            var size = new Vector2(textSize.X + padding * 2f, Line);

            ImGui.SetCursorScreenPos(pos with { X = x });
            var clicked = ImGui.InvisibleButton(group.Name, size);
            var hovered = ImGui.IsItemHovered();

            if (active || hovered)
            {
                drawList.AddRectFilled(pos with { X = x }, new Vector2(x + size.X, pos.Y + Line), Color(1f, 1f, 1f, active ? 0.12f : 0.06f));
            }

            var textColor = active ? Color(1f, 1f, 1f) : hovered ? Color(0.85f, 0.85f, 0.9f) : Color(0.6f, 0.6f, 0.65f);
            drawList.AddText(new Vector2(x + padding, pos.Y) + TextOffset, textColor, group.Name);

            if (clicked)
            {
                _path.Clear();
                _path.Add(group.Name);
            }

            x += size.X + padding;
        }
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

        drawList.AddText(pos - new Vector2(0f, Line), Color(0.8f, 0.8f, 0.85f), $"{label}  {total.TimeElapsedMs[selected]:F2} ms   avg {total.AverageTimeElapsedMs:F2}");

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

    private float DrawLegend(ImDrawListPtr drawList, Vector2 pos, float width, IReadOnlyList<ProfilerNode> series, int selected)
    {
        var x = pos.X;
        var y = pos.Y;
        var right = x + width;
        var edge = 0.1f * Unit;
        var indent = Square + Gap;

        // fake ".." entry to drill back up the hierarchy, if we're not at the root.
        if (_path.Count > 1)
        {
            var accentColor = Color(0.30f, 0.55f, 0.95f);

            ImGui.SetCursorScreenPos(new Vector2(x, y));
            if (ImGui.InvisibleButton("legendUp", new Vector2(width, Line)))
                _path.RemoveAt(_path.Count - 1);
            var hoveredUp = ImGui.IsItemHovered();

            drawList.AddRectFilled(new Vector2(x - edge, y), new Vector2(right + edge, y + Line), Color(0.30f, 0.55f, 0.95f, hoveredUp ? 0.22f : 0.12f));
            drawList.AddRectFilled(new Vector2(x - edge, y), new Vector2(x, y + Line), accentColor);

            var upColor = hoveredUp ? Color(1f, 1f, 1f) : Color(0.85f, 0.88f, 0.95f);
            drawList.AddText(new Vector2(x + indent, y) + TextOffset, upColor, "..");

            y += Line;
        }

        for (var t = 0; t < series.Count; t++)
        {
            var task = series[t];
            var drillable = task.Children.Count > 0;

            // Whole-row hit target so a click drills into the zone's sub-timings.
            if (drillable)
            {
                ImGui.SetCursorScreenPos(new Vector2(x, y));
                if (ImGui.InvisibleButton($"legend{t}", new Vector2(width, Line)))
                    _path.Add(task.Name);
                if (ImGui.IsItemHovered())
                    drawList.AddRectFilled(new Vector2(x - edge, y), new Vector2(right + edge, y + Line), Color(1f, 1f, 1f, 0.06f));
            }

            var cpuMs = task.Cpu.TimeElapsedMs[selected];
            var gpuMs = task.HasGpu ? task.Gpu.TimeElapsedMs[selected] : 0f;

            var sqTop = new Vector2(x, y + (Line - Square) / 2f);
            drawList.AddRectFilled(sqTop, sqTop + new Vector2(Square, Square), _palette[t % _palette.Length]);

            drawList.AddText(new Vector2(x + indent, y) + TextOffset, Color(0.85f, 0.85f, 0.88f), task.Name);
            if (drillable)
                drawList.AddText(new Vector2(x + indent + ImGui.CalcTextSize(task.Name).X + edge * 2f, y) + TextOffset, Color(0.5f, 0.5f, 0.55f), Settings.AngleRightIcon);

            var timing = task.HasGpu ? $"{cpuMs:F2} / {gpuMs:F2} ms" : $"{cpuMs:F2} ms";
            var timingWidth = ImGui.CalcTextSize(timing).X;
            drawList.AddText(new Vector2(right - timingWidth, y) + TextOffset, Color(0.62f, 0.62f, 0.68f), timing);

            if (task.HasPrimitives)
            {
                var primitives = FormatCount(task.TotalPrimitives);
                var primitivesWidth = ImGui.CalcTextSize(primitives).X;
                drawList.AddText(new Vector2(right - timingWidth - Gap - primitivesWidth, y) + TextOffset, Color(0.45f, 0.45f, 0.52f), primitives);
            }

            y += Line;
        }

        return y;
    }

    private static ProfilerMetricData Series(ProfilerNode node, bool gpu) => gpu ? node.Gpu : node.Cpu;

    /// <summary>Primitive counts run to millions and the card is narrow, so they get short-scaled.</summary>
    private static string FormatCount(long count) => count switch
    {
        >= 1_000_000_000 => $"{count / 1_000_000_000.0:0.##}B",
        >= 1_000_000 => $"{count / 1_000_000.0:0.##}M",
        >= 1_000 => $"{count / 1_000.0:0.##}K",
        _ => count.ToString()
    };

    private static uint Color(float r, float g, float b, float a = 1f) => (uint)(a * 255f) << 24 | (uint)(b * 255f) << 16 | (uint)(g * 255f) << 8 | (uint)(r * 255f);
}
