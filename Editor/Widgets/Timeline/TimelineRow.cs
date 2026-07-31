using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Editor.Widgets.Timeline;

internal enum TimelineRowKind
{
    Component,
    Sequence,

    /// <summary>Every notify of an animation on one line, and the parent of its per-track rows.</summary>
    NotifyGroup,
    Notifies,

    /// <summary>Every curve of an animation on one line, and the parent of its per-curve rows.</summary>
    CurveGroup,
    Curve
}

/// <summary>
/// One line of the timeline. Rows are built only when the actor's shape changes, so anything a row can
/// work out about itself from the asset is worked out once here rather than every frame: what a curve
/// is keyed over, where its animation is busy, and how its names read cut to the gutter.
/// </summary>
internal sealed class TimelineRow
{
    public required TimelineRowKind Kind;
    public required int Depth;
    public required ActorComponent Component;
    public string Label = string.Empty;
    public string Detail = string.Empty;
    public string BarLabel = string.Empty;
    public int Index;    // sequence index, notify track index, or the curve's place in the group
    public bool Expandable;
    public bool Expanded;

    /// <summary>What a curve row is plotted against, which its own keys decide and never changes.</summary>
    public float CurveMin;
    public float CurveMax;

    /// <summary>When the curves of a group row are doing something, and how much.</summary>
    public TimelineCurves.Activity[] CurveActivity = [];

    /// <summary>The clock the row runs on, which every row of a component shares.</summary>
    public SkeletalMeshComponent? Skeletal => Component as SkeletalMeshComponent;

    public AnimationDescriptor? Animation => Skeletal?.Animation;

    public SequenceDescriptor? Sequence => Kind == TimelineRowKind.Sequence && Animation is { } animation && Index < animation.Sequences.Length
        ? animation.Sequences[Index]
        : null;

    /// <summary>A prop the performance moves rather than a component performing it.</summary>
    public bool Driven => Component is SpatialComponent { Relation: SkeletalMeshComponent };

    public TimelinePalette Palette => Driven ? TimelineStyle.Driven : TimelineStyle.Own;

    /// <summary>Only an animated component carries a toggle, and only those carry no detail.</summary>
    public bool HasToggle => Kind == TimelineRowKind.Component && Animation != null;

    public bool Selectable => Kind is TimelineRowKind.Component or TimelineRowKind.Sequence;

    /// <summary>A curve row reads out what it is worth right now, which no other row has to.</summary>
    public bool HasReadout => Kind == TimelineRowKind.Curve;

    private string _elidedLabel = string.Empty;
    private string _elidedBar = string.Empty;
    private float _labelWidth = float.NaN;
    private float _barWidth = float.NaN;

    /// <summary>
    /// The names cut to what they are given, remembered until that width changes. Eliding measures the
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

    public string FitBarLabel(float width)
    {
        if (width == _barWidth) return _elidedBar;

        _barWidth = width;
        _elidedBar = TimelineStyle.Elide(BarLabel, width);
        return _elidedBar;
    }
}
