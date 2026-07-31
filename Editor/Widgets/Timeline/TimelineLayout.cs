using ImGuiNET;

namespace Editor.Widgets.Timeline;

/// <summary>
/// Where the timeline puts things, measured once a frame so the ruler and the rows cannot disagree
/// about where time starts. It is taken from inside the row child rather than out in the window: only
/// the child knows what its padding and its scrollbar have left it, and that width is exactly the one
/// a row's tree node spans.
/// </summary>
internal sealed class TimelineLayout
{
    public float Duration;
    public float TrackX;
    public float TrackWidth;
    public float RowWidth;
    public float RowHeight;
    public float TextPadY;   // the node's own frame padding, which is what its text sits on
    public float ArrowWidth; // the column the tree node reserves for its arrow

    /// <summary>Pixels a second is worth, so plotting a curve costs no division per point.</summary>
    private float _scale;

    public void Measure(float duration)
    {
        var style = ImGui.GetStyle();

        // the rows are real tree nodes, so their height is the framed item height rather than ours to
        // pick, and what one costs the scroll is that plus the spacing left behind it
        Duration = duration;
        RowHeight = ImGui.GetFrameHeight();
        TextPadY = style.FramePadding.Y;
        ArrowWidth = ImGui.GetFontSize() + style.FramePadding.X * 2f;
        RowWidth = ImGui.GetContentRegionAvail().X;
        TrackX = ImGui.GetCursorScreenPos().X + TimelineStyle.NameWidth;
        TrackWidth = MathF.Max(1f, RowWidth - TimelineStyle.NameWidth);

        _scale = duration > 0f ? TrackWidth / duration : 0f;
    }

    /// <summary>Screen x of a point in time, clamped to the track.</summary>
    public float TimeToX(float time) => TrackX + Math.Clamp(time * _scale, 0f, TrackWidth);

    /// <summary>And back, for the cursor and for a point sampled at a pixel.</summary>
    public float XToTime(float x) => (x - TrackX) / TrackWidth * Duration;
}
