using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors.Animations;

namespace Editor.Widgets.Timeline;

internal enum TimelineRowKind
{
    /// <summary>An animation: its sections or clips on the track, and the parent of everything below.</summary>
    Header,

    /// <summary>One slot of a montage with its clips, only when the slot says more than the header already does.</summary>
    Slot,

    /// <summary>Every notify of the animation on one line, and the parent of its per-track rows.</summary>
    NotifyGroup,
    Notifies,

    /// <summary>Every curve of the animation on one line, and the parent of its per-curve rows.</summary>
    CurveGroup,
    Curve,

    /// <summary>A skeletal mesh of the actor playing the animation on a clock of its own, a prop a performance pulled in included.</summary>
    Player,

    /// <summary>A skinned mesh wearing the player's pose, with no clock of its own.</summary>
    Follower
}

/// <summary>One animation the actor plays, and the clocks of everything playing it.</summary>
internal sealed class TimelineBlock(SequenceBaseDescriptor animation)
{
    public readonly SequenceBaseDescriptor Animation = animation;
    public readonly List<AnimationPlayback> Clocks = [];

    /// <summary>The block's own position, which is its first clock: what its clips, markers and curves are read against.</summary>
    public float Time => Clocks[0].Time;
}

/// <summary>
/// One line of the timeline. Rows are built only when the actor's shape changes, so anything a row can
/// work out about itself from the asset is worked out once here rather than every frame.
/// </summary>
internal sealed class TimelineRow
{
    public required TimelineRowKind Kind;
    public required TimelineBlock Block;
    public int Depth;
    public ActorComponent? Component;
    public AnimationPlayback? Clock; // a player's own, dragged to seek it alone; a follower carries its leader's
    public string Label = string.Empty;
    public string Detail = string.Empty;
    public int Index;    // the slot's, notify track's or curve's place among its kind, which is what tells their rows apart
    public bool Expandable;
    public bool Expanded;

    /// <summary>What a curve row is plotted against, which its own keys decide and never changes.</summary>
    public float CurveMin;
    public float CurveMax;

    /// <summary>When the curves of a group row are doing something, and how much.</summary>
    public TimelineCurves.Activity[] CurveActivity = [];

    /// <summary>What a slot row draws, gathered with the row rather than filtered by name every frame.</summary>
    public SegmentDescriptor[] Segments = [];

    public SequenceBaseDescriptor Animation => Block.Animation;

    public int Id => Component?.Id ?? Animation.Path.GetHashCode();

    public bool Selectable => Component is not null;

    private string _elidedLabel = string.Empty;
    private float _labelWidth = float.NaN;

    /// <summary>
    /// The name cut to what it is given, remembered until that width changes. Eliding measures the
    /// text a handful of times to find the cut, and a row that has not been resized would find the
    /// same one every frame.
    /// </summary>
    public string FitLabel(float width)
    {
        if (width == _labelWidth) return _elidedLabel;

        _labelWidth = width;
        _elidedLabel = TimelineStyle.Elide(Label, width);
        return _elidedLabel;
    }
}
