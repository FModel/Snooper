using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors.Animations;

namespace Editor.Widgets.Timeline;

/// <summary>
/// Everything a row draws to the right of the gutter: sections and clips, markers, plots, and the
/// clocks. The window has already clipped this to the row, so nothing here has to.
/// </summary>
internal static class TimelineTrack
{
    /// <param name="time">The block's own position, which its clips, markers and curves are read against.</param>
    /// <param name="value">What a curve row is worth under that clock, measured once for the row.</param>
    public static void Draw(ImDrawListPtr drawList, TimelineLayout layout, TimelineRow row, Vector2 origin, float time, float? value)
    {
        var top = origin.Y + TimelineStyle.BarInset;
        var bottom = origin.Y + layout.RowHeight - TimelineStyle.BarInset;
        var animation = row.Animation;

        switch (row.Kind)
        {
            case TimelineRowKind.Header:
            {
                // a montage reads as its sections, anything else as its clips
                if (animation is MontageDescriptor { Sections.Length: > 0 } montage)
                {
                    for (var i = 0; i < montage.Sections.Length; i++)
                    {
                        DrawSection(drawList, layout, montage.Sections[i], i, time, top, bottom, origin.Y);
                    }
                }
                else
                {
                    for (var i = 0; i < animation.Segments.Count; i++)
                    {
                        DrawSegment(drawList, layout, animation.Segments[i], i, time, top, bottom, origin.Y);
                    }
                }
                break;
            }
            case TimelineRowKind.Slot:
            {
                drawList.AddRectFilled(new Vector2(layout.TimeToX(0f), top), new Vector2(layout.TimeToX(animation.Duration), bottom), ImGui.GetColorU32(TimelineStyle.Track));

                for (var i = 0; i < row.Segments.Length; i++)
                {
                    DrawSegment(drawList, layout, row.Segments[i], i, time, top, bottom, origin.Y);
                }
                break;
            }
            case TimelineRowKind.NotifyGroup or TimelineRowKind.Notifies:
            {
                var spans = row.Kind == TimelineRowKind.Notifies;
                DrawGroupLine(drawList, layout, animation, top, bottom);

                foreach (var notify in animation.Notifies)
                {
                    // the group carries every lane at once, so the shape survives being collapsed
                    if (spans && notify.TrackIndex != row.Index) continue;

                    DrawNotify(drawList, layout, notify, top, bottom, spans);
                }
                break;
            }
            case TimelineRowKind.CurveGroup:
            {
                // the notify group's line, since this row is read the same way: not for a shape but
                // for when the thing under it happens
                TimelineCurves.DrawActivity(drawList, layout, row.CurveActivity, DrawGroupLine(drawList, layout, animation, top, bottom));
                break;
            }
            case TimelineRowKind.Curve:
            {
                drawList.AddRectFilled(new Vector2(layout.TimeToX(0f), top), new Vector2(layout.TimeToX(animation.Duration), bottom), ImGui.GetColorU32(TimelineStyle.Track));
                TimelineCurves.DrawPlot(drawList, layout, row, animation, time, value, top, bottom);
                break;
            }
            case TimelineRowKind.Player when row.Clock is { } clock:
            {
                DrawClock(drawList, layout, clock, top, bottom, origin.Y);
                break;
            }
            case TimelineRowKind.Follower when row.Clock is { } leader:
            {
                // dotted up to the leader's time: the follower is wherever its leader is
                var middle = (top + bottom) * 0.5f;
                var end = layout.TimeToX(leader.Time);
                var color = ImGui.GetColorU32(TimelineStyle.Dim);
                for (var x = layout.TimeToX(0f); x < end; x += 6f)
                {
                    drawList.AddLine(new Vector2(x, middle), new Vector2(MathF.Min(x + 3f, end), middle), color);
                }
                break;
            }
        }
    }

    /// <summary>
    /// The one scale every block is drawn to, in the strip above the rows, and the actor's playhead on
    /// it: the first clock of its first animation.
    /// </summary>
    public static void DrawRuler(ImDrawListPtr drawList, TimelineLayout layout, float top, float bottom, float time)
    {
        var dim = ImGui.GetColorU32(TimelineStyle.Dim);

        var step = TimelineStyle.TickSteps[^1];
        foreach (var candidate in TimelineStyle.TickSteps)
        {
            if (candidate / layout.Duration * layout.TrackWidth < TimelineStyle.MinTickGap) continue;

            step = candidate;
            break;
        }

        var left = layout.TrackX - TimelineStyle.NameWidth;
        drawList.AddLine(new Vector2(left, bottom), new Vector2(left + layout.RowWidth, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));

        for (var t = 0f; t <= layout.Duration + 0.0001f; t += step)
        {
            var x = MathF.Round(layout.TimeToX(t));
            drawList.AddLine(new Vector2(x, bottom - 4f), new Vector2(x, bottom), dim);
            drawList.AddText(new Vector2(x + 3f, top), dim, $"{t:0.##}s");
        }

        var headX = MathF.Round(layout.TimeToX(time));
        var color = ImGui.GetColorU32(TimelineStyle.Head);
        drawList.AddLine(new Vector2(headX, top), new Vector2(headX, bottom), color);
        drawList.AddTriangleFilled(new Vector2(headX - 4f, bottom - 5f), new Vector2(headX + 4f, bottom - 5f), new Vector2(headX, bottom), color);
    }

    /// <summary>
    /// A clock: the stretch it has played as a bar with its time on it, a dot where it is, and its rate
    /// when that is not the plain one. The dot is what the row is dragged by.
    /// </summary>
    private static void DrawClock(ImDrawListPtr drawList, TimelineLayout layout, AnimationPlayback clock, float top, float bottom, float rowY)
    {
        var left = layout.TimeToX(0f);
        var right = layout.TimeToX(clock.Duration);
        var x = layout.TimeToX(clock.Time);
        var middle = (top + bottom) * 0.5f;
        var head = ImGui.GetColorU32(clock.IsPlaying ? TimelineStyle.Head : TimelineStyle.Dim);

        drawList.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), ImGui.GetColorU32(TimelineStyle.Track));
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(x, bottom), ImGui.GetColorU32(clock.IsPlaying ? TimelineStyle.Active : TimelineStyle.Bar));

        // the time rides the end of the bar, inside it while it fits and past the dot before that
        var text = $"{clock.Time:0.00}s";
        var width = ImGui.CalcTextSize(text).X;
        var textX = x - left > width + 12f ? x - width - 8f : x + 8f;
        drawList.AddText(new Vector2(textX, rowY + layout.TextPadY), ImGui.GetColorU32(TimelineStyle.Text), text);

        drawList.AddCircleFilled(new Vector2(x, middle), (bottom - top) * 0.3f, head);

        if (MathF.Abs(clock.PlayRate - 1f) <= 0.001f) return;

        // semibold at a smaller size, the same trick the hardware band uses to stay legible when it
        // has to sit on top of something else
        var rate = $"{clock.PlayRate:0.##}x";
        var font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        var fontSize = ImGui.GetFontSize() * TimelineStyle.RateFontScale;
        var rateWidth = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, rate).X;
        var rateX = textX > x ? textX + width + 6f : x + 8f;
        if (rateX + rateWidth > layout.TrackX + layout.TrackWidth) rateX = textX - rateWidth - 6f;

        drawList.AddText(font, fontSize, new Vector2(rateX, rowY + layout.TextPadY + 1f), ImGui.GetColorU32(TimelineStyle.Rate), rate);
    }

    /// <summary>The bed a group row's markers sit on, and the height they sit at.</summary>
    private static float DrawGroupLine(ImDrawListPtr drawList, TimelineLayout layout, SequenceBaseDescriptor animation, float top, float bottom)
    {
        var middle = (top + bottom) * 0.5f;
        drawList.AddLine(new Vector2(layout.TimeToX(0f), middle), new Vector2(layout.TimeToX(animation.Duration), middle), ImGui.GetColorU32(TimelineStyle.Track));
        return middle;
    }

    /// <summary>
    /// One clip, named on its own box the way a clip is labelled in any editing timeline. Cut to that
    /// box rather than elided: a row draws several of them at widths that move with the zoom, so the
    /// tooltip is what a box too narrow to name is read with.
    /// </summary>
    private static void DrawSegment(ImDrawListPtr drawList, TimelineLayout layout, SegmentDescriptor segment, int index, float time, float top, float bottom, float rowY)
    {
        var left = layout.TimeToX(segment.StartPos);
        var right = layout.TimeToX(segment.EndPos);
        var fill = segment.IsActiveAt(time) ? TimelineStyle.Active : index % 2 == 0 ? TimelineStyle.Bar : TimelineStyle.BarAlt;

        // the hairline that keeps neighbours apart is taken off the near side, so a clip closing the
        // animation still closes the track
        drawList.AddRectFilled(new Vector2(left + (segment.StartPos > 0f ? 1f : 0f), top), new Vector2(right, bottom), ImGui.GetColorU32(fill));

        drawList.PushClipRect(new Vector2(left, rowY), new Vector2(right, rowY + layout.RowHeight), true);
        drawList.AddText(new Vector2(left + 5f, rowY + layout.TextPadY), ImGui.GetColorU32(TimelineStyle.Text), segment.Sequence.Name);

        if (segment.LoopCount > 1)
        {
            var text = $"{Settings.LoopIcon} {segment.LoopCount}";
            var width = ImGui.CalcTextSize(text).X;
            drawList.AddText(new Vector2(right - width - 5f, rowY + layout.TextPadY), ImGui.GetColorU32(TimelineStyle.Rate), text);
        }

        drawList.PopClipRect();
    }

    /// <summary>
    /// One montage section, filled like a clip because it is read for the same thing: which of them the
    /// clock is inside. Sections meet end to end, so the row reads as a strip cut into parts. A section
    /// naming itself as next carries the loop glyph, that being the whole of what holds an animation on it.
    /// </summary>
    private static void DrawSection(ImDrawListPtr drawList, TimelineLayout layout, SectionDescriptor section, int index, float time, float top, float bottom, float rowY)
    {
        var left = layout.TimeToX(section.StartTime);
        var right = layout.TimeToX(section.EndTime);
        var fill = section.IsActiveAt(time) ? TimelineStyle.Active : index % 2 == 0 ? TimelineStyle.Bar : TimelineStyle.BarAlt;

        drawList.AddRectFilled(new Vector2(left + (section.StartTime > 0f ? 1f : 0f), top), new Vector2(right, bottom), ImGui.GetColorU32(fill));

        drawList.PushClipRect(new Vector2(left, rowY), new Vector2(right, rowY + layout.RowHeight), true);
        drawList.AddText(new Vector2(left + 5f, rowY + layout.TextPadY), ImGui.GetColorU32(TimelineStyle.Text), section.Name);

        if (section.NextIndex == index)
        {
            var glyph = ImGui.CalcTextSize(Settings.LoopIcon).X;
            drawList.AddText(new Vector2(right - glyph - 5f, rowY + layout.TextPadY), ImGui.GetColorU32(TimelineStyle.Rate), Settings.LoopIcon);
        }

        drawList.PopClipRect();
    }

    private static void DrawNotify(ImDrawListPtr drawList, TimelineLayout layout, NotifyDescriptor notify, float top, float bottom, bool spans)
    {
        var color = ImGui.GetColorU32(TimelineStyle.Notify);
        var start = layout.TimeToX(notify.TriggerTime);

        if (spans && notify.IsState)
        {
            drawList.AddRectFilled(new Vector2(start, top), new Vector2(layout.TimeToX(notify.TriggerTime + notify.Duration), bottom), ImGui.GetColorU32(TimelineStyle.Notify with { W = 0.35f }));
        }

        var middle = (top + bottom) * 0.5f;
        drawList.AddTriangleFilled(new Vector2(start, middle - TimelineStyle.NotifySize), new Vector2(start + TimelineStyle.NotifySize, middle), new Vector2(start, middle + TimelineStyle.NotifySize), color);
        drawList.AddTriangleFilled(new Vector2(start, middle - TimelineStyle.NotifySize), new Vector2(start - TimelineStyle.NotifySize, middle), new Vector2(start, middle + TimelineStyle.NotifySize), color);
    }
}
