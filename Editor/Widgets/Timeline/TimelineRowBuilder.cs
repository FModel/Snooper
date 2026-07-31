using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;

namespace Editor.Widgets.Timeline;

/// <summary>
/// The rows of the timeline and the clocks they run on, rebuilt only when the actor changes shape:
/// which components it carries, what they are playing, and which of the groups are open. Everything
/// else about a performance moves without changing that shape, so the list survives the frame.
/// </summary>
internal sealed class TimelineRowBuilder
{
    private readonly List<TimelineRow> _rows = [];
    private readonly List<SkeletalMeshComponent> _clocks = [];
    private readonly List<string> _curveNames = [];
    private readonly List<int> _notifyTracks = [];

    // the groups open independently of the component they hang off and of each other, so each needs
    // its own set, all three being keyed by the same component id. The group sets hold what is open
    // and this one what is shut, which is what leaves every component expanded and every group closed
    // to begin with: a montage carries far more of both than the window has rows
    private readonly HashSet<int> _collapsed = [];
    private readonly HashSet<int> _expandedNotifies = [];
    private readonly HashSet<int> _expandedCurves = [];

    private Actor? _actor;
    private int _signature;
    private bool _dirty = true;

    public IReadOnlyList<TimelineRow> Rows => _rows;
    public IReadOnlyList<SkeletalMeshComponent> Clocks => _clocks;
    public float Duration { get; private set; }

    /// <summary>
    /// Brings the rows up to date. Nothing an animation does as it plays reaches this: only the actor
    /// being swapped, a component coming or going, a new animation on one of them, or an arrow being
    /// clicked, which is what leaves the list standing from one frame to the next.
    /// </summary>
    public void Refresh(Actor actor)
    {
        var signature = Signature(actor);
        if (!_dirty && ReferenceEquals(actor, _actor) && signature == _signature) return;

        _actor = actor;
        _signature = signature;
        _dirty = false;

        Build(actor);
    }

    /// <summary>Records what an arrow just did, and that the rows under it have to be laid out again.</summary>
    public void SetExpanded(TimelineRow row, bool open)
    {
        if (row.Kind is TimelineRowKind.NotifyGroup or TimelineRowKind.CurveGroup)
        {
            var groups = row.Kind == TimelineRowKind.NotifyGroup ? _expandedNotifies : _expandedCurves;
            if (open) groups.Add(row.Component.Id);
            else groups.Remove(row.Component.Id);
        }
        else if (open) _collapsed.Remove(row.Component.Id);
        else _collapsed.Add(row.Component.Id);

        _dirty = true;
    }

    /// <summary>
    /// What the rows are laid out from, cheap enough to ask every frame: the components the actor
    /// carries, what each is animating, and what it hangs off. A montage playing through changes none
    /// of it, which is the point.
    /// </summary>
    private static int Signature(Actor actor)
    {
        var hash = new HashCode();

        // indexed rather than enumerated, an observable collection handing out a boxed enumerator
        for (var i = 0; i < actor.Components.Count; i++)
        {
            var component = actor.Components[i];
            hash.Add(component.Id);
            hash.Add(component is SkeletalMeshComponent { Animation: { } animation } ? animation.GetHashCode() : 0);
            hash.Add((component as SpatialComponent)?.Relation?.Id ?? 0);
        }

        return hash.ToHashCode();
    }

    private void Build(Actor actor)
    {
        _rows.Clear();
        _clocks.Clear();
        Duration = 0f;

        for (var i = 0; i < actor.Components.Count; i++)
        {
            // a component driven by another skeletal mesh is shown under it, not at the top
            if (actor.Components[i] is not SkeletalMeshComponent { Relation: not SkeletalMeshComponent } skeletal) continue;

            AddComponent(skeletal, 0);
        }

        // nothing here is animated, so there is no timeline to show
        if (Duration <= 0f)
        {
            _rows.Clear();
            _clocks.Clear();
        }
    }

    private void AddComponent(ActorComponent component, int depth)
    {
        var skeletal = component as SkeletalMeshComponent;
        var animation = skeletal?.Animation;
        var children = CollectChildren(component);

        if (skeletal != null)
        {
            // every animated component runs its own clock, so each one is something the transport drives
            _clocks.Add(skeletal);
            Duration = MathF.Max(Duration, animation?.Duration ?? 0f);
        }

        var expandable = children.Count > 0 || animation is { Sequences.Length: > 0 } or { Notifies.Length: > 0 };
        var expanded = expandable && !_collapsed.Contains(component.Id);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.Component,
            Depth = depth,
            Component = component,
            Label = component.Name,
            BarLabel = DescribeBar(component, animation),
            Expandable = expandable,
            Expanded = expanded
        });

        if (!expanded) return;

        if (animation != null)
        {
            for (var i = 0; i < animation.Sequences.Length; i++)
            {
                var sequence = animation.Sequences[i];
                _rows.Add(new TimelineRow
                {
                    Kind = TimelineRowKind.Sequence,
                    Depth = depth + 1,
                    Component = component,
                    Label = sequence.SlotName,
                    Detail = $"{sequence.Duration:0.00}s",
                    BarLabel = sequence.Name,
                    Index = i
                });
            }

            AddNotifies(component, animation, depth + 1);
            AddCurves(component, animation, depth + 1);
        }

        foreach (var child in children)
        {
            AddComponent(child, depth + 1);
        }
    }

    /// <summary>
    /// The notifies of an animation: one group row carrying all of them, which opens into a row per
    /// track the animator laid out. A track almost always holds a single notify, so those rows are
    /// named after it rather than after the lane number.
    /// </summary>
    private void AddNotifies(ActorComponent component, AnimationDescriptor animation, int depth)
    {
        if (animation.Notifies.Length == 0) return;

        var expanded = _expandedNotifies.Contains(component.Id);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.NotifyGroup,
            Depth = depth,
            Component = component,
            Label = "Notifies",
            Detail = $"{animation.Notifies.Length}",
            Expandable = true,
            Expanded = expanded
        });

        if (!expanded) return;

        depth++;
        _notifyTracks.Clear();
        foreach (var notify in animation.Notifies)
        {
            if (!_notifyTracks.Contains(notify.TrackIndex)) _notifyTracks.Add(notify.TrackIndex);
        }
        _notifyTracks.Sort();

        foreach (var track in _notifyTracks)
        {
            string? name = null;
            var count = 0;
            foreach (var notify in animation.Notifies)
            {
                if (notify.TrackIndex != track) continue;

                name ??= notify.Name;
                count++;
            }

            _rows.Add(new TimelineRow
            {
                Kind = TimelineRowKind.Notifies,
                Depth = depth,
                Component = component,
                Label = name ?? "Notify",
                Detail = count > 1 ? $"+{count - 1}" : string.Empty,
                Index = track
            });
        }
    }

    /// <summary>
    /// The float curves an animation carries: one group row, which opens into a row per curve. A curve
    /// belongs to a sequence, so a montage can key the same name on several of them; the rows are the
    /// union of those names, each plotted over whichever spans hold it.
    /// </summary>
    private void AddCurves(ActorComponent component, AnimationDescriptor animation, int depth)
    {
        _curveNames.Clear();
        foreach (var sequence in animation.Sequences)
        {
            if (sequence.Curves is not { } curves) continue;

            foreach (var name in curves.Keys)
            {
                if (!_curveNames.Contains(name)) _curveNames.Add(name);
            }
        }

        if (_curveNames.Count == 0) return;

        _curveNames.Sort(StringComparer.OrdinalIgnoreCase);

        var expanded = _expandedCurves.Contains(component.Id);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.CurveGroup,
            Depth = depth,
            Component = component,
            Label = "Curves",
            Detail = $"{_curveNames.Count}",
            Expandable = true,
            Expanded = expanded,
            CurveActivity = TimelineCurves.CollectActivity(animation)
        });

        if (!expanded) return;

        depth++;
        for (var i = 0; i < _curveNames.Count; i++)
        {
            var (min, max) = TimelineCurves.Range(animation, _curveNames[i]);
            _rows.Add(new TimelineRow
            {
                Kind = TimelineRowKind.Curve,
                Depth = depth,
                Component = component,
                Label = _curveNames[i],
                Index = i,
                CurveMin = min,
                CurveMax = max
            });
        }
    }

    /// <summary>
    /// Whatever this component drives: the props, weapons and sounds an animation pulled in, which all
    /// point back at it through <see cref="TransformComponent.Relation"/>.
    /// </summary>
    private static List<ActorComponent> CollectChildren(ActorComponent component)
    {
        var children = new List<ActorComponent>();
        if (component.Actor is not { } actor) return children;

        for (var i = 0; i < actor.Components.Count; i++)
        {
            var candidate = actor.Components[i];
            if (candidate == component) continue;
            if (candidate is SpatialComponent { Relation: { } relation } && relation == component)
            {
                children.Add(candidate);
            }
        }

        return children;
    }

    private static string DescribeBar(ActorComponent component, AnimationDescriptor? animation)
    {
        if (animation != null) return animation.Name;

        return component is SpatialComponent { AttachSocketName: { Length: > 0 } socket } ? socket : "attached";
    }
}
