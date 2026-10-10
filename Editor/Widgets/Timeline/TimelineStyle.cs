using System.Numerics;
using ImGuiNET;

namespace Editor.Widgets.Timeline;

/// <summary>
/// What the timeline is measured and inked in, and the two pieces of chrome every part of it shares.
/// The inks are the hardware overlay's family, which works because every hue is a vivid accent over a
/// dark fill rather than a wash of mid tones: same inks here, same reason.
/// </summary>
internal static class TimelineStyle
{
    public const string Title = Snooper.Settings.TimelineWindow;
    public const string PlayIcon = "\uf04b";      // play
    public const string PauseIcon = "\uf04c";     // pause
    public const string RewindIcon = "\uf049";    // fast-backward
    public const string ExportIcon = "\uf56e";    // file-export

    public const float NameWidth = 210f;   // the left gutter holding the component tree
    public static float RulerHeight => ImGui.GetFrameHeight(); // the transport's buttons sit in it, so it is a row of them
    public const float IndentWidth = 12f;
    public const float BarInset = 2f;      // vertical gap between a bar and its row
    public const float MinTickGap = 64f;   // smallest pixel gap between two ruler labels
    public const float NotifySize = 4f;
    public const float GhostAlpha = 0.04f; // the placeholder timeline behind an empty window
    public const float RateFontScale = 0.82f;

    public static readonly float[] TickSteps = [0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f];

    /// <summary>Widths of the placeholder clips, as a fraction of the track. Uneven on purpose.</summary>
    public static readonly float[] GhostSpans = [0.94f, 0.52f, 0.71f, 0.28f, 0.63f, 0.41f, 0.85f, 0.36f];

    public static readonly Vector4 Text = new(0.86f, 0.88f, 0.90f, 1f);
    public static readonly Vector4 Dim = new(0.42f, 0.46f, 0.52f, 1f);
    public static readonly Vector4 Track = new(1f, 1f, 1f, 0.05f);

    /// <summary>What a row sits on, by kind: an animation's row is a band, a player's a lighter one, the rest sit on the window.</summary>
    public static readonly Vector4 HeaderBand = new(1f, 1f, 1f, 0.07f);
    public static readonly Vector4 PlayerBand = new(1f, 1f, 1f, 0.03f);
    public static readonly Vector4 Notify = new(0.95f, 0.75f, 0.25f, 1f);
    public static readonly Vector4 Curve = new(0.72f, 0.56f, 0.98f, 1f);

    /// <summary>
    /// The play rate, which lands wherever its tick happens to be and so has no fill to dress for. The
    /// brightest ink in the family, because it has to carry over a bar and its label both. It shares
    /// the notify amber but never a row with one, and a number reads nothing like a diamond.
    /// </summary>
    public static readonly Vector4 Rate = new(0.92f, 0.82f, 0.18f, 1f);

    /// <summary>The clips and sections, on the overlay's blue, and <see cref="Head"/> for a clock's own position.</summary>
    public static readonly Vector4 Bar = new(0.14f, 0.22f, 0.40f, 1f);
    public static readonly Vector4 BarAlt = new(0.17f, 0.27f, 0.48f, 1f);
    public static readonly Vector4 Active = new(0.23f, 0.36f, 0.62f, 1f);
    public static readonly Vector4 Head = new(0.38f, 0.62f, 0.98f, 1f);

    /// <summary>
    /// Frameless square toggle, the default button chrome would drown a strip this small.
    /// </summary>
    public static bool IconButton(string id, string label, bool active, string tooltip, Vector2? bounds = null)
    {
        var size = bounds ?? new Vector2(ImGui.GetFrameHeight());
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        if (active || hovered)
        {
            drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(Text with { W = active ? 0.18f : 0.08f }));
        }

        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(origin + (size - textSize) * 0.5f, ImGui.GetColorU32(active || hovered ? Text : Text with { W = 0.45f }), label);

        if (hovered && tooltip.Length > 0) ImGui.SetTooltip(tooltip);

        return clicked;
    }

    /// <summary>
    /// Text cut to a width with a trailing ellipsis. Asset names are far longer than any gutter, and
    /// silently overlapping the next column is worse than losing the tail. Measuring is what this
    /// costs, so callers hold on to what comes back rather than asking again every frame.
    /// </summary>
    public static string Elide(string text, float maxWidth)
    {
        if (maxWidth <= 0f || text.Length == 0) return string.Empty;
        if (ImGui.CalcTextSize(text).X <= maxWidth) return text;

        const string ellipsis = "...";
        var budget = maxWidth - ImGui.CalcTextSize(ellipsis).X;
        if (budget <= 0f) return string.Empty;

        // binary search so a long name costs a handful of measures rather than one per character
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..middle]).X <= budget) low = middle;
            else high = middle - 1;
        }

        return low > 0 ? text[..low] + ellipsis : string.Empty;
    }
}
