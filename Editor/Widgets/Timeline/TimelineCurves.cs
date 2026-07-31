using System.Numerics;
using CUE4Parse.UE4.Objects.Engine.Curves;
using ImGuiNET;
using Snooper.Rendering.Components.Descriptors;

namespace Editor.Widgets.Timeline;

/// <summary>
/// The float curves an animation carries, read and drawn. Everything a curve can say about itself
/// without knowing the time is worked out when the rows are built; what is left here per frame is the
/// value under the clock and the strokes themselves.
/// </summary>
internal static class TimelineCurves
{
    // no two samples of a curve land closer together than this, which is what keeps a plot down to the
    // points its own row can show: a stroked point costs vertices, and a draw list indexes them with
    // sixteen bits for the whole window
    private const float Step = 4f;
    private const int Columns = 128;  // most points a group row sorts its activity into,
    private const float DotGap = 8f;  // and how far apart it would rather space them
    private const float DotSize = 2f;
    private const float HeadSize = 2.5f;

    /// <summary>A moment the curves of an animation move, and how far they move at it.</summary>
    internal readonly record struct Activity(float Time, float Weight);

    /// <summary>
    /// The range a curve is keyed over, across every sequence that carries the name. Taken from the
    /// keys rather than by sampling, which is what keeps a plot from swimming as the clock moves
    /// through it.
    /// </summary>
    public static (float Min, float Max) Range(AnimationDescriptor animation, string name)
    {
        var min = float.MaxValue;
        var max = float.MinValue;

        foreach (var sequence in animation.Sequences)
        {
            if (sequence.Curves is not { } curves || !curves.TryGetValue(name, out var curve)) continue;

            Extend(curve, ref min, ref max);
        }

        return min <= max ? (min, max) : (0f, 1f);
    }

    /// <summary>The curve's value on whichever sequence holds that point in time, if any does.</summary>
    public static float? Value(AnimationDescriptor animation, string name, float time)
    {
        foreach (var sequence in animation.Sequences)
        {
            if (time < sequence.StartTime || time >= sequence.EndTime) continue;
            if (sequence.Curves is not { } curves || !curves.TryGetValue(name, out var curve)) return null;

            return curve.Eval(sequence.ToLocalTime(time));
        }

        return null;
    }

    /// <summary>
    /// When the curves of an animation are doing something, and how much, on the montage's own clock.
    /// A key on its own says nothing, a curve holding a value keying as often as one sweeping through
    /// it, so what is measured is how far the value moved. That is taken against the curve's own range
    /// so one keyed in centimetres cannot drown one keyed in the nought to one a blend weight lives in.
    /// </summary>
    public static Activity[] CollectActivity(AnimationDescriptor animation)
    {
        var samples = new List<Activity>();

        foreach (var sequence in animation.Sequences)
        {
            if (sequence.Curves is not { } curves) continue;

            foreach (var curve in curves.Values)
            {
                var keys = curve.Keys;
                if (keys.Length < 2) continue;

                var min = float.MaxValue;
                var max = float.MinValue;
                Extend(curve, ref min, ref max);

                // a curve that holds one value for the whole animation never does anything
                var range = max - min;
                if (range <= 0.0001f) continue;

                for (var i = 1; i < keys.Length; i++)
                {
                    var moved = MathF.Abs(keys[i].Value - keys[i - 1].Value) / range;
                    if (moved <= 0.0001f) continue;

                    samples.Add(new Activity(sequence.FromLocalTime(keys[i].Time), moved));
                }
            }
        }

        return samples.Count > 0 ? samples.ToArray() : [];
    }

    /// <summary>
    /// What a group row carries in place of the plots themselves, which stacked dozens deep are only a
    /// smear: a point wherever the curves under it are doing something, the brighter for how much.
    /// </summary>
    public static void DrawActivity(ImDrawListPtr drawList, TimelineLayout layout, Activity[] samples, float line)
    {
        if (samples.Length == 0) return;

        // the points are spaced rather than packed, so a narrow panel holds fewer of them than a wide
        // one instead of running them together into the strip this is meant not to be
        var count = Math.Clamp((int) (layout.TrackWidth / DotGap), 1, Columns);
        var columns = (stackalloc float[Columns])[..count];

        var peak = 0f;
        foreach (var sample in samples)
        {
            var ratio = (layout.TimeToX(sample.Time) - layout.TrackX) / layout.TrackWidth;
            var column = Math.Clamp((int) (ratio * count), 0, count - 1);

            columns[column] += sample.Weight;
            peak = MathF.Max(peak, columns[column]);
        }

        if (peak <= 0f) return;

        var width = layout.TrackWidth / count;
        for (var i = 0; i < count; i++)
        {
            if (columns[i] <= 0f) continue;

            // against the busiest point of the row, and off the square root of it: one moment of the
            // animation almost always dwarfs the rest, and a straight ramp leaves everything else dark
            var heat = MathF.Sqrt(columns[i] / peak);
            var color = ImGui.GetColorU32(TimelineStyle.Curve with { W = 0.22f + 0.68f * heat });
            drawList.AddCircleFilled(new Vector2(layout.TrackX + (i + 0.5f) * width, line), DotSize, color, 6);
        }
    }

    /// <summary>
    /// Every span of a curve on one row, and the value under the clock marked on it so the number in
    /// the gutter has a place on the plot.
    /// </summary>
    public static void DrawPlot(ImDrawListPtr drawList, TimelineLayout layout, TimelineRow row, AnimationDescriptor animation, float local, float? value, float top, float bottom)
    {
        var color = ImGui.GetColorU32(TimelineStyle.Curve);

        foreach (var sequence in animation.Sequences)
        {
            if (sequence.Curves is not { } curves || !curves.TryGetValue(row.Label, out var curve)) continue;

            Stroke(drawList, layout, sequence, curve, row.CurveMin, row.CurveMax, top, bottom, color);
        }

        if (value is { } under)
        {
            drawList.AddCircleFilled(new Vector2(layout.TimeToX(local), Y(under, row.CurveMin, row.CurveMax, top, bottom)), HeadSize, color);
        }
    }

    /// <summary>
    /// One curve over the span of the sequence that keys it, sampled off its keys rather than off the
    /// pixels: a key is where the shape actually turns, so a linear one is worth a single point and
    /// only a cubic segment needs anything drawn between two of them. What that costs is bounded by
    /// the row: a curve baked at frame rate keys far more often than the bar can show, and those keys
    /// are dropped as they are reached rather than counted up front.
    /// </summary>
    private static void Stroke(ImDrawListPtr drawList, TimelineLayout layout, SequenceDescriptor sequence, FRichCurve curve, float min, float max, float top, float bottom, uint color)
    {
        var keys = curve.Keys;
        if (keys.Length == 0) return;

        var left = layout.TimeToX(sequence.StartTime);
        var right = layout.TimeToX(sequence.EndTime);
        if (right - left < 2f) return;

        // the plot spans the whole bar: a curve holds its end values either side of its own keys
        var last = left;
        Sample(left);

        for (var i = 0; i < keys.Length; i++)
        {
            var x = KeyX(keys[i].Time);

            // a key landing on the pixel the last one drew has nothing left to say about the shape.
            // Only the keys are measured this way, so what a segment draws between them cannot make
            // the one closing it look crowded and drop it
            if (x - last < Step) continue;

            last = x;
            Sample(x);

            if (i + 1 >= keys.Length) continue;

            var next = KeyX(keys[i + 1].Time);
            if (next - x <= Step) continue;

            switch (keys[i].InterpMode)
            {
                // a step has to be held to the next key, or joining the samples ramps it instead
                case ERichCurveInterpMode.RCIM_Constant:
                    Sample(next - 1f);
                    break;
                case ERichCurveInterpMode.RCIM_Cubic:
                    var segments = (int) ((next - x) / Step);
                    for (var s = 1; s < segments; s++)
                    {
                        Sample(x + (next - x) * s / segments);
                    }
                    break;
            }
        }

        Sample(right);

        // a hairline, which is the one stroke ImGui draws without doubling the vertices for width
        drawList.PathStroke(color, ImDrawFlags.None, 1f);
        return;

        float KeyX(float local) => Math.Clamp(layout.TimeToX(sequence.FromLocalTime(local)), left, right);

        void Sample(float x) => drawList.PathLineTo(new Vector2(x, Y(curve.Eval(sequence.ToLocalTime(layout.XToTime(x))), min, max, top, bottom)));
    }

    /// <summary>
    /// Where a value sits in the row it is plotted on. A curve with no range to be read against, which
    /// most of them are for most of their length, holds the middle rather than an edge.
    /// </summary>
    private static float Y(float value, float min, float max, float top, float bottom)
    {
        var span = max - min;
        if (span <= 0.0001f) return (top + bottom) * 0.5f;

        // cubic keys overshoot the range their own values describe, and flattening that against the
        // row is better than the stroke vanishing into the clip rect
        return Math.Clamp(bottom - (value - min) / span * (bottom - top), top, bottom);
    }

    private static void Extend(FRichCurve curve, ref float min, ref float max)
    {
        foreach (var key in curve.Keys)
        {
            min = MathF.Min(min, key.Value);
            max = MathF.Max(max, key.Value);
        }
    }
}
