using Snooper;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Descriptors.Animations;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets.Timeline;

/// <summary>
/// The blocks and rows of the selected actor, rebuilt only when it changes shape: a component coming
/// or going, an animation set or cleared on one, a leader taken, or an arrow being clicked. Everything
/// else about a performance moves without changing that shape, so the list survives the frame.
/// </summary>
internal sealed class TimelineRows
{
    private readonly List<TimelineBlock> _blocks = [];
    private readonly List<TimelineRow> _rows = [];
    private readonly List<string> _curveNames = [];
    private readonly List<string> _slotNames = [];
    private readonly List<int> _notifyTracks = [];

    // blocks and players start open, the notify and curve groups shut: a montage carries far more of
    // those than the window has rows
    private readonly HashSet<string> _collapsedBlocks = [];
    private readonly HashSet<int> _collapsedPlayers = [];
    private readonly HashSet<string> _expandedNotifies = [];
    private readonly HashSet<string> _expandedCurves = [];

    private Actor? _actor;
    private int _signature;
    private bool _dirty = true;

    /// <summary>Every animation the actor's components play, the props' clips among them.</summary>
    public IReadOnlyList<TimelineBlock> Blocks => _blocks;
    public IReadOnlyList<TimelineRow> Rows => _rows;

    /// <summary>The longest animation on show, which is the scale everything is drawn to.</summary>
    public float Duration { get; private set; }

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
        var key = row.Animation.Path;
        switch (row.Kind)
        {
            case TimelineRowKind.Header:
                if (open) _collapsedBlocks.Remove(key);
                else _collapsedBlocks.Add(key);
                break;
            case TimelineRowKind.NotifyGroup:
                if (open) _expandedNotifies.Add(key);
                else _expandedNotifies.Remove(key);
                break;
            case TimelineRowKind.CurveGroup:
                if (open) _expandedCurves.Add(key);
                else _expandedCurves.Remove(key);
                break;
            case TimelineRowKind.Player when row.Component is { } component:
                if (open) _collapsedPlayers.Remove(component.Id);
                else _collapsedPlayers.Add(component.Id);
                break;
        }

        _dirty = true;
    }

    private static int Signature(Actor actor)
    {
        var hash = new HashCode();

        // indexed rather than enumerated, an observable collection handing out a boxed enumerator
        for (var i = 0; i < actor.Components.Count; i++)
        {
            if (actor.Components[i] is not SkinnedMeshComponent skinned) continue;

            hash.Add(skinned.Id);
            hash.Add(skinned.Leader?.Id ?? 0);
            hash.Add(skinned.Followers.Count);
            if (skinned is SkeletalMeshComponent skeletal) hash.Add(skeletal.Animation?.GetHashCode() ?? 0);
        }

        return hash.ToHashCode();
    }

    /// <summary>Plays on a clock of its own, the actor's own meshes and the props its performances pull in alike: a follower wears someone else's pose.</summary>
    private static bool IsPlayer(SkeletalMeshComponent component) => component is { Animation: not null, Leader: null };

    private void Build(Actor actor)
    {
        _blocks.Clear();
        _rows.Clear();
        Duration = 0f;

        for (var i = 0; i < actor.Components.Count; i++)
        {
            if (actor.Components[i] is not SkeletalMeshComponent component || !IsPlayer(component)) continue;

            var animation = component.Animation!;
            var block = _blocks.Find(x => x.Animation == animation);
            if (block is null)
            {
                block = new TimelineBlock(animation);
                _blocks.Add(block);
            }

            if (!block.Clocks.Contains(component.Playback!)) block.Clocks.Add(component.Playback!);
        }

        foreach (var block in _blocks)
        {
            AddBlock(block, actor);
        }
    }

    private void AddBlock(TimelineBlock block, Actor actor)
    {
        var animation = block.Animation;
        var expanded = !_collapsedBlocks.Contains(animation.Path);

        Duration = MathF.Max(Duration, animation.Duration);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.Header,
            Block = block,
            Label = animation.Name,
            Expandable = true,
            Expanded = expanded
        });

        if (!expanded) return;

        AddSlots(block, 1);
        AddNotifies(block, 1);
        AddCurves(block, 1);

        for (var i = 0; i < actor.Components.Count; i++)
        {
            if (actor.Components[i] is SkeletalMeshComponent component && IsPlayer(component) && component.Animation == animation) AddPlayer(block, component);
        }
    }

    private void AddPlayer(TimelineBlock block, SkeletalMeshComponent component)
    {
        var expandable = component.Followers.Count > 0;
        var expanded = expandable && !_collapsedPlayers.Contains(component.Id);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.Player,
            Block = block,
            Depth = 1,
            Component = component,
            Clock = component.Playback,
            Label = component.Name,
            Expandable = expandable,
            Expanded = expanded
        });

        if (!expanded) return;

        foreach (var follower in component.Followers)
        {
            _rows.Add(new TimelineRow
            {
                Kind = TimelineRowKind.Follower,
                Block = block,
                Depth = 2,
                Component = follower,
                Clock = component.Playback,
                Label = $"{Settings.LinkIcon}  {follower.Name}"
            });
        }
    }

    /// <summary>
    /// The slots a montage plays on, one row each, only where they say more than the header: several
    /// slots, or a slot cut into several clips. A sequence is one clip on one slot and shows none.
    /// </summary>
    private void AddSlots(TimelineBlock block, int depth)
    {
        var animation = block.Animation;

        _slotNames.Clear();
        foreach (var segment in animation.Segments)
        {
            if (!_slotNames.Contains(segment.SlotName)) _slotNames.Add(segment.SlotName);
        }

        if (_slotNames.Count < 2 && animation.Segments.Count < 2) return;

        for (var i = 0; i < _slotNames.Count; i++)
        {
            var slot = _slotNames[i];
            var segments = new List<SegmentDescriptor>();
            foreach (var segment in animation.Segments)
            {
                if (segment.SlotName == slot) segments.Add(segment);
            }

            _rows.Add(new TimelineRow
            {
                Kind = TimelineRowKind.Slot,
                Block = block,
                Depth = depth,
                Index = i,
                Label = slot,
                // one segment can say how long it holds the slot, several can only say how many
                Detail = segments.Count > 1 ? $"{segments.Count}" : $"{segments[0].Duration:0.00}s",
                Segments = segments.ToArray()
            });
        }
    }

    /// <summary>
    /// The notifies of an animation: one group row carrying all of them, which opens into a row per
    /// track the animator laid out. A track almost always holds a single notify, so those rows are
    /// named after it rather than after the lane number.
    /// </summary>
    private void AddNotifies(TimelineBlock block, int depth)
    {
        var animation = block.Animation;
        if (animation.Notifies.Length == 0) return;

        var expanded = _expandedNotifies.Contains(animation.Path);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.NotifyGroup,
            Block = block,
            Depth = depth,
            Label = "Notifies",
            Detail = $"{animation.Notifies.Length}",
            Expandable = true,
            Expanded = expanded
        });

        if (!expanded) return;

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
                Block = block,
                Depth = depth + 1,
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
    private void AddCurves(TimelineBlock block, int depth)
    {
        var animation = block.Animation;

        _curveNames.Clear();
        foreach (var segment in animation.Segments)
        {
            if (segment.Sequence.Curves is not { } curves) continue;

            foreach (var name in curves.Keys)
            {
                if (!_curveNames.Contains(name)) _curveNames.Add(name);
            }
        }

        if (_curveNames.Count == 0) return;

        _curveNames.Sort(StringComparer.OrdinalIgnoreCase);

        var expanded = _expandedCurves.Contains(animation.Path);

        _rows.Add(new TimelineRow
        {
            Kind = TimelineRowKind.CurveGroup,
            Block = block,
            Depth = depth,
            Label = "Curves",
            Detail = $"{_curveNames.Count}",
            Expandable = true,
            Expanded = expanded,
            CurveActivity = TimelineCurves.CollectActivity(animation)
        });

        if (!expanded) return;

        for (var i = 0; i < _curveNames.Count; i++)
        {
            var (min, max) = TimelineCurves.Range(animation, _curveNames[i]);
            _rows.Add(new TimelineRow
            {
                Kind = TimelineRowKind.Curve,
                Block = block,
                Depth = depth + 1,
                Label = _curveNames[i],
                Index = i,
                CurveMin = min,
                CurveMax = max
            });
        }
    }
}
