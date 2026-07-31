using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components.Descriptors;

namespace Editor.Widgets.Timeline;

/// <summary>
/// Everything a row draws to the right of the gutter: its bars, its markers, its plot, and the clock
/// it runs on. The window has already clipped this to the row, so nothing here has to.
/// </summary>
internal static class TimelineTrack
{
    /// <param name="head">The actor's own position, which every row is read against.</param>
    /// <param name="local">And this row's, which a driven prop will have drifted away from.</param>
    /// <param name="value">What a curve row is worth under its clock, measured once for the row.</param>
    public static void Draw(ImDrawListPtr drawList, TimelineLayout layout, TimelineRow row, Vector2 origin, float head, float local, float? value)
    {
        var top = origin.Y + TimelineStyle.BarInset;
        var bottom = origin.Y + layout.RowHeight - TimelineStyle.BarInset;
        var palette = row.Palette;

        switch (row.Kind)
        {
            case TimelineRowKind.Component when row.Animation is { } animation:
            {
                drawList.AddRectFilled(new Vector2(layout.TimeToX(0f), top), new Vector2(layout.TimeToX(animation.Duration), bottom), ImGui.GetColorU32(TimelineStyle.Track));

                for (var i = 0; i < animation.Sequences.Length; i++)
                {
                    DrawSequenceBar(drawList, layout, animation.Sequences[i], local, i % 2 == 0 ? palette.Bar : palette.BarAlt, palette, top, bottom);
                }

                // without sequences the bar is only the faint track fill, which cannot carry white
                DrawBarLabel(drawList, layout, row, layout.TimeToX(0f), layout.TimeToX(animation.Duration), origin.Y, animation.Sequences.Length > 0 ? TimelineStyle.Text : TimelineStyle.Dim);
                break;
            }
            case TimelineRowKind.Component:
            {
                // driven but not animated: it is simply attached for the whole of its driver's animation
                drawList.AddRectFilled(new Vector2(layout.TimeToX(0f), top), new Vector2(layout.TimeToX(layout.Duration), bottom), ImGui.GetColorU32(palette.Bar with { W = 0.55f }));
                DrawBarLabel(drawList, layout, row, layout.TimeToX(0f), layout.TimeToX(layout.Duration), origin.Y, TimelineStyle.Dim);
                break;
            }
            case TimelineRowKind.Sequence when row.Sequence is { } sequence:
            {
                DrawSequenceBar(drawList, layout, sequence, local, palette.Bar, palette, top, bottom);
                DrawBarLabel(drawList, layout, row, layout.TimeToX(sequence.StartTime), layout.TimeToX(sequence.EndTime), origin.Y, TimelineStyle.Text);
                break;
            }
            case TimelineRowKind.NotifyGroup or TimelineRowKind.Notifies when row.Animation is { } animation:
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
            case TimelineRowKind.CurveGroup when row.Animation is { } animation:
            {
                // the notify group's line, since this row is read the same way: not for a shape but
                // for when the thing under it happens
                TimelineCurves.DrawActivity(drawList, layout, row.CurveActivity, DrawGroupLine(drawList, layout, animation, top, bottom));
                break;
            }
            case TimelineRowKind.Curve when row.Animation is { } animation:
            {
                drawList.AddRectFilled(new Vector2(layout.TimeToX(0f), top), new Vector2(layout.TimeToX(animation.Duration), bottom), ImGui.GetColorU32(TimelineStyle.Track));
                TimelineCurves.DrawPlot(drawList, layout, row, animation, local, value, top, bottom);
                break;
            }
        }

        // this row's clock, in its own colour, and only once it has left the actor's playhead behind
        var x = MathF.Round(layout.TimeToX(local));
        if (MathF.Abs(local - head) > 0.001f)
        {
            drawList.AddLine(new Vector2(x, top), new Vector2(x, bottom), ImGui.GetColorU32(palette.Head));
        }

        if (row is { Kind: TimelineRowKind.Component, Animation: { } playing })
        {
            DrawRate(drawList, layout, playing.PlayRate, x, origin.Y);
        }
    }

    /// <summary>The bed a group row's markers sit on, and the height they sit at.</summary>
    private static float DrawGroupLine(ImDrawListPtr drawList, TimelineLayout layout, AnimationDescriptor animation, float top, float bottom)
    {
        var middle = (top + bottom) * 0.5f;
        drawList.AddLine(new Vector2(layout.TimeToX(0f), middle), new Vector2(layout.TimeToX(animation.Duration), middle), ImGui.GetColorU32(TimelineStyle.Track));
        return middle;
    }

    /// <summary>
    /// One sequence span, lifted a step while the clock is inside it. The lift stops well short of the
    /// accent so the fill still carries a white label. The hairline that keeps neighbours apart is
    /// taken off the near side, so a bar closing the animation still closes the track.
    /// </summary>
    private static void DrawSequenceBar(ImDrawListPtr drawList, TimelineLayout layout, SequenceDescriptor sequence, float local, Vector4 fill, TimelinePalette palette, float top, float bottom)
    {
        var active = local >= sequence.StartTime && local < sequence.EndTime;

        drawList.AddRectFilled(
            new Vector2(layout.TimeToX(sequence.StartTime) + (sequence.StartTime > 0f ? 1f : 0f), top),
            new Vector2(layout.TimeToX(sequence.EndTime), bottom),
            ImGui.GetColorU32(active ? palette.Active : fill));
    }

    /// <summary>
    /// The asset name rides on its own bar, the way a clip is labelled in any editing timeline. It is
    /// the only column wide enough to hold one.
    /// </summary>
    private static void DrawBarLabel(ImDrawListPtr drawList, TimelineLayout layout, TimelineRow row, float left, float right, float rowY, Vector4 color)
    {
        if (row.BarLabel.Length == 0) return;

        drawList.PushClipRect(new Vector2(left, rowY), new Vector2(right, rowY + layout.RowHeight), true);
        drawList.AddText(new Vector2(left + 5f, rowY + layout.TextPadY), ImGui.GetColorU32(color), row.FitBarLabel(right - left - 8f));
        drawList.PopClipRect();
    }

    /// <summary>
    /// How fast the row is playing, riding its clock tick since the tick is the thing moving at that
    /// rate. Only worth the ink when it is not 1x, which since props got their own clocks no longer
    /// means the whole actor was retimed.
    /// </summary>
    private static void DrawRate(ImDrawListPtr drawList, TimelineLayout layout, float rate, float tickX, float rowY)
    {
        if (Math.Abs(rate - 1f) <= 0.001f) return;

        // semibold at a smaller size, the same trick the hardware band uses to stay legible when it
        // has to sit on top of something else
        var text = $"{rate:0.##}x";
        var font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        var fontSize = ImGui.GetFontSize() * TimelineStyle.RateFontScale;
        var width = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, text).X;

        // reads on the near side of the tick rather than run off the end of the track
        var right = layout.TrackX + layout.TrackWidth;
        var x = tickX + 4f + width <= right ? tickX + 4f : tickX - 4f - width;

        drawList.AddText(font, fontSize, new Vector2(x, rowY + layout.TextPadY + 1f), ImGui.GetColorU32(TimelineStyle.Rate), text);
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
