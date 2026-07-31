using System.Numerics;
using ImGuiNET;

namespace Editor.Widgets.Timeline;

/// <summary>
/// What the window shows with nothing to play: a ghost of the timeline it would be drawing, so the
/// panel still reads as a timeline instead of an empty box, with the reason centred on it.
/// </summary>
internal static class TimelineEmptyState
{
    public static void Draw(string headline, string hint)
    {
        var origin = ImGui.GetCursorScreenPos();
        var size = ImGui.GetContentRegionAvail();
        if (size.X <= 0f || size.Y <= 0f) return;

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = ImGui.GetFrameHeight();

        drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));

        var headlineSize = ImGui.CalcTextSize(headline);
        var hintSize = ImGui.CalcTextSize(hint);
        var height = headlineSize.Y + 4f + hintSize.Y;
        var center = origin.X + size.X * 0.5f;
        var top = origin.Y + (size.Y - height) * 0.5f;

        // a panel narrower than its own gutter has no track left to place anything on
        if (size.X > TimelineStyle.NameWidth * 1.5f)
        {
            DrawGhost(drawList, origin, size, rowHeight, origin.Y + size.Y * 0.5f, height * 0.5f + 4f);
        }

        drawList.AddText(new Vector2(center - headlineSize.X * 0.5f, top), ImGui.GetColorU32(TimelineStyle.Text), headline);
        drawList.AddText(new Vector2(center - hintSize.X * 0.5f, top + headlineSize.Y + 4f), ImGui.GetColorU32(TimelineStyle.Dim), hint);

        ImGui.Dummy(size);
    }

    /// <summary>
    /// The placeholder rows: a stub in the gutter and a clip on the track, the shape every real row
    /// has. They fade out as they near the message instead of stopping dead against it, which in a
    /// short panel left a couple of stray bars and read as a hole punched through the ghost.
    /// </summary>
    private static void DrawGhost(ImDrawListPtr drawList, Vector2 origin, Vector2 size, float rowHeight, float messageCenter, float messageRadius)
    {
        var trackX = origin.X + TimelineStyle.NameWidth;
        var trackWidth = size.X - TimelineStyle.NameWidth - 12f;
        var bottom = origin.Y + size.Y;
        var falloff = rowHeight * 3f;

        drawList.AddLine(origin with { Y = origin.Y + TimelineStyle.RulerHeight }, new Vector2(origin.X + size.X, origin.Y + TimelineStyle.RulerHeight), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, TimelineStyle.GhostAlpha)));

        var y = origin.Y + TimelineStyle.RulerHeight;
        for (var i = 0; y + rowHeight <= bottom; i++, y += rowHeight)
        {
            var distance = MathF.Abs(y + rowHeight * 0.5f - messageCenter) - messageRadius;
            var alpha = TimelineStyle.GhostAlpha * Math.Clamp(distance / falloff, 0f, 1f);
            if (alpha <= 0f) continue;

            var ghost = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
            var indent = i % 3 == 0 ? 0f : TimelineStyle.IndentWidth;
            var span = TimelineStyle.GhostSpans[i % TimelineStyle.GhostSpans.Length];

            // the gutter divider comes in row-sized pieces so that it fades along with them
            drawList.AddLine(new Vector2(trackX, y), new Vector2(trackX, y + rowHeight), ghost);
            drawList.AddRectFilled(new Vector2(origin.X + 6f + indent, y + TimelineStyle.BarInset + 2f), new Vector2(origin.X + TimelineStyle.NameWidth * (0.35f + span * 0.35f), y + rowHeight - TimelineStyle.BarInset - 2f), ghost);
            drawList.AddRectFilled(new Vector2(trackX + 6f + indent, y + TimelineStyle.BarInset), new Vector2(trackX + 6f + span * trackWidth, y + rowHeight - TimelineStyle.BarInset), ghost);
        }
    }
}
