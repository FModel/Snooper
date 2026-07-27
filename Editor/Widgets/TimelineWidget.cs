using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Editor.Widgets;

/// <summary>
/// Playback view of the selected actor, tied to <see cref="SkinnedMeshRenderSystem"/>: what it is
/// playing, when each sequence and notify lands, and what those animations drive through the
/// components attached to them. The transport drives that actor's clocks only, so pausing or
/// seeking one performance leaves every other actor in the scene running.
/// </summary>
public class TimelineWidget
{
    private const string Title = "\ue0e4 Timeline";
    private const string PlayIcon = "\uf04b"; // play
    private const string PauseIcon = "\uf04c"; // pause
    private const string RewindIcon = "\uf049"; // fast-backward
    private const string ExpandedIcon = "\uf0d7"; // caret-down
    private const string CollapsedIcon = "\uf0da"; // caret-right

    private const float NameWidth = 210f;  // the left gutter holding the component tree
    private const float RulerHeight = 18f;
    private const float RowPadY = 3f;
    private const float IndentWidth = 12f;
    private const float CaretWidth = 16f;
    private const float BarInset = 2f;     // vertical gap between a bar and its row
    private const float MinTickGap = 64f;  // smallest pixel gap between two ruler labels
    private const float NotifySize = 4f;
    private const float GhostAlpha = 0.04f; // the placeholder timeline behind an empty window

    private static readonly float[] _tickSteps = [0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f];

    /// <summary>Widths of the placeholder clips, as a fraction of the track. Uneven on purpose.</summary>
    private static readonly float[] _ghostSpans = [0.94f, 0.52f, 0.71f, 0.28f, 0.63f, 0.41f, 0.85f, 0.36f];

    // the hardware overlay's family, which works because every hue is a vivid accent over a dark
    // fill rather than a wash of mid tones: same inks here, same reason
    private static readonly Vector4 _textColor = new(0.86f, 0.88f, 0.90f, 1f);
    private static readonly Vector4 _dimColor = new(0.42f, 0.46f, 0.52f, 1f);
    private static readonly Vector4 _trackColor = new(1f, 1f, 1f, 0.05f);
    private static readonly Vector4 _notifyColor = new(0.95f, 0.75f, 0.25f, 1f);

    /// <summary>
    /// The play rate, which lands wherever its tick happens to be and so has no fill to dress for.
    /// The brightest ink in the family, because it has to carry over a bar and its label both. It
    /// shares the notify amber but never a row with one, and a number reads nothing like a diamond.
    /// </summary>
    private static readonly Vector4 _rateColor = new(0.92f, 0.82f, 0.18f, 1f);
    private const float RateFontScale = 0.82f;

    /// <summary>
    /// The colours a row is drawn in, chosen by whose clock it runs on. <see cref="Head"/> is that
    /// clock's own position and doubles as the row's accent, being the vivid end of its hue: a driven
    /// prop keeps its own time now, so it runs at its own rate and holds at its own end.
    /// </summary>
    private readonly record struct Palette(Vector4 Bar, Vector4 BarAlt, Vector4 Active, Vector4 Head);

    /// <summary>The actor's own performance, on the overlay's blue.</summary>
    private static readonly Palette _ownPalette = new(
        new Vector4(0.14f, 0.22f, 0.40f, 1f),
        new Vector4(0.17f, 0.27f, 0.48f, 1f),
        new Vector4(0.23f, 0.36f, 0.62f, 1f),
        new Vector4(0.38f, 0.62f, 0.98f, 1f));

    /// <summary>Anything that performance drives, on the overlay's green.</summary>
    private static readonly Palette _drivenPalette = new(
        new Vector4(0.12f, 0.28f, 0.20f, 1f),
        new Vector4(0.15f, 0.34f, 0.25f, 1f),
        new Vector4(0.20f, 0.46f, 0.33f, 1f),
        new Vector4(0.36f, 0.76f, 0.52f, 1f));

    private enum RowKind
    {
        Component,
        Sequence,

        /// <summary>Every notify of an animation on one line, and the parent of its per-track rows.</summary>
        NotifyGroup,
        Notifies
    }

    private sealed class Row
    {
        public RowKind Kind;
        public int Depth;
        public string Label = string.Empty;
        public string Detail = string.Empty;
        public string BarLabel = string.Empty;
        public required ActorComponent Component;
        public SkeletalMeshComponent? Skeletal;
        public AnimationDescriptor? Animation;
        public int Index;    // sequence index, or notify track index
        public bool Driven;  // runs on its own clock under another skeletal mesh
        public bool Expandable;
        public bool Expanded;

        public Palette Palette => Driven ? _drivenPalette : _ownPalette;
    }

    private readonly List<Row> _rows = [];
    private readonly List<SkeletalMeshComponent> _clocks = [];
    private readonly HashSet<int> _collapsed = [];

    // notify groups open independently of the component they hang off, so they need their own set,
    // both being keyed by the same component id. This one holds what is open rather than what is
    // shut, which is what starts every group closed: the markers already show on the group's own row
    private readonly HashSet<int> _expandedNotifies = [];
    private readonly List<int> _notifyTracks = [];

    private Actor? _lastActor;
    private ActorComponent? _lastSelected;
    private int _scrollTarget = -1;

    // frame layout, measured once so the ruler and the rows cannot disagree about where time starts
    private float _duration;
    private float _trackX;
    private float _trackWidth;
    private float _rowWidth;
    private float _rowHeight;

    public void Draw(InterfaceManager manager)
    {
        if (!ImGui.Begin(Title))
        {
            ImGui.End();
            return;
        }

        var system = manager.GetSystem<SkinnedMeshRenderSystem>();
        if (system == null)
        {
            DrawEmpty("Nothing to play", "This scene has no skinned meshes.");
            ImGui.End();
            return;
        }

        var actor = manager.SelectedActor ?? manager.SelectedComponent?.Actor;
        if (actor == null)
        {
            DrawEmpty("No actor selected", "Pick one in the hierarchy or the viewport.");
            ImGui.End();
            return;
        }

        BuildRows(actor);
        if (_rows.Count == 0)
        {
            DrawEmpty("Nothing animated", $"{actor.Name} has no component playing an animation.");
            ImGui.End();
            return;
        }

        TrackSelection(manager, actor);
        DrawTransport(actor);
        UpdateLayout();
        DrawRuler();
        DrawRows(manager);

        ImGui.End();
    }

    /// <summary>
    /// What the window shows with nothing to play: a ghost of the timeline it would be drawing, so
    /// the panel still reads as a timeline instead of an empty box, with the reason centred on it.
    /// </summary>
    private static void DrawEmpty(string headline, string hint)
    {
        var origin = ImGui.GetCursorScreenPos();
        var size = ImGui.GetContentRegionAvail();
        if (size.X <= 0f || size.Y <= 0f) return;

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = ImGui.GetTextLineHeight() + RowPadY * 2f;

        drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));

        var headlineSize = ImGui.CalcTextSize(headline);
        var hintSize = ImGui.CalcTextSize(hint);
        var height = headlineSize.Y + 4f + hintSize.Y;
        var center = origin.X + size.X * 0.5f;
        var top = origin.Y + (size.Y - height) * 0.5f;

        // a panel narrower than its own gutter has no track left to place anything on
        if (size.X > NameWidth * 1.5f)
        {
            DrawGhost(drawList, origin, size, rowHeight, origin.Y + size.Y * 0.5f, height * 0.5f + 4f);
        }

        drawList.AddText(new Vector2(center - headlineSize.X * 0.5f, top), ImGui.GetColorU32(_textColor), headline);
        drawList.AddText(new Vector2(center - hintSize.X * 0.5f, top + headlineSize.Y + 4f), ImGui.GetColorU32(_dimColor), hint);

        ImGui.Dummy(size);
    }

    /// <summary>
    /// The placeholder rows: a stub in the gutter and a clip on the track, the shape every real row
    /// has. They fade out as they near the message instead of stopping dead against it, which in a
    /// short panel left a couple of stray bars and read as a hole punched through the ghost.
    /// </summary>
    private static void DrawGhost(ImDrawListPtr drawList, Vector2 origin, Vector2 size, float rowHeight, float messageCenter, float messageRadius)
    {
        var trackX = origin.X + NameWidth;
        var trackWidth = size.X - NameWidth - 12f;
        var bottom = origin.Y + size.Y;
        var falloff = rowHeight * 3f;

        drawList.AddLine(origin with { Y = origin.Y + RulerHeight }, new Vector2(origin.X + size.X, origin.Y + RulerHeight), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, GhostAlpha)));

        var y = origin.Y + RulerHeight;
        for (var i = 0; y + rowHeight <= bottom; i++, y += rowHeight)
        {
            var distance = MathF.Abs(y + rowHeight * 0.5f - messageCenter) - messageRadius;
            var alpha = GhostAlpha * Math.Clamp(distance / falloff, 0f, 1f);
            if (alpha <= 0f) continue;

            var ghost = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
            var indent = i % 3 == 0 ? 0f : IndentWidth;
            var span = _ghostSpans[i % _ghostSpans.Length];

            // the gutter divider comes in row-sized pieces so that it fades along with them
            drawList.AddLine(new Vector2(trackX, y), new Vector2(trackX, y + rowHeight), ghost);
            drawList.AddRectFilled(new Vector2(origin.X + 6f + indent, y + BarInset + 2f), new Vector2(origin.X + NameWidth * (0.35f + span * 0.35f), y + rowHeight - BarInset - 2f), ghost);
            drawList.AddRectFilled(new Vector2(trackX + 6f + indent, y + BarInset), new Vector2(trackX + 6f + span * trackWidth, y + rowHeight - BarInset), ghost);
        }
    }

    private void BuildRows(Actor actor)
    {
        _rows.Clear();
        _clocks.Clear();
        _duration = 0f;

        foreach (var component in actor.Components)
        {
            // a component driven by another skeletal mesh is shown under it, not at the top
            if (component is not SkeletalMeshComponent { Relation: not SkeletalMeshComponent } skeletal) continue;

            AddComponentRow(skeletal, 0);
        }

        // nothing here is animated, so there is no timeline to show
        if (_duration <= 0f)
        {
            _rows.Clear();
            _clocks.Clear();
        }
    }

    private void AddComponentRow(ActorComponent component, int depth)
    {
        var skeletal = component as SkeletalMeshComponent;
        var animation = skeletal?.Animation;
        var children = CollectChildren(component);
        var driven = component is SpatialComponent { Relation: SkeletalMeshComponent };

        if (skeletal != null)
        {
            // every animated component runs its own clock, so each one is something the transport drives
            _clocks.Add(skeletal);
            _duration = MathF.Max(_duration, animation?.Duration ?? 0f);
        }

        var expandable = children.Count > 0 || animation is { Sequences.Length: > 0 } or { Notifies.Length: > 0 };
        var expanded = expandable && !_collapsed.Contains(component.Id);

        _rows.Add(new Row
        {
            Kind = RowKind.Component,
            Depth = depth,
            Label = component.Name,
            BarLabel = DescribeBar(component, animation),
            Component = component,
            Skeletal = skeletal,
            Animation = animation,
            Driven = driven,
            Expandable = expandable,
            Expanded = expanded
        });

        if (!expanded) return;

        if (animation != null)
        {
            for (var i = 0; i < animation.Sequences.Length; i++)
            {
                var sequence = animation.Sequences[i];
                _rows.Add(new Row
                {
                    Kind = RowKind.Sequence,
                    Depth = depth + 1,
                    Label = sequence.SlotName,
                    Detail = $"{sequence.Duration:0.00}s",
                    BarLabel = sequence.Name,
                    Component = component,
                    Skeletal = skeletal,
                    Animation = animation,
                    Driven = driven,
                    Index = i
                });
            }

            AddNotifyRows(component, skeletal, animation, depth + 1, driven);
        }

        foreach (var child in children)
        {
            AddComponentRow(child, depth + 1);
        }
    }

    /// <summary>
    /// The notifies of an animation: one group row carrying all of them, which opens into a row per
    /// track the animator laid out. A track almost always holds a single notify, so those rows are
    /// named after it rather than after the lane number.
    /// </summary>
    private void AddNotifyRows(ActorComponent component, SkeletalMeshComponent? skeletal, AnimationDescriptor animation, int depth, bool driven)
    {
        if (animation.Notifies.Length == 0) return;

        var expanded = _expandedNotifies.Contains(component.Id);

        _rows.Add(new Row
        {
            Kind = RowKind.NotifyGroup,
            Depth = depth,
            Label = "Notifies",
            Detail = $"{animation.Notifies.Length}",
            Component = component,
            Skeletal = skeletal,
            Animation = animation,
            Driven = driven,
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

            _rows.Add(new Row
            {
                Kind = RowKind.Notifies,
                Depth = depth,
                Label = name ?? "Notify",
                Detail = count > 1 ? $"+{count - 1}" : string.Empty,
                Component = component,
                Skeletal = skeletal,
                Animation = animation,
                Driven = driven,
                Index = track
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

        foreach (var candidate in actor.Components)
        {
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

    /// <summary>
    /// Reveals a component picked elsewhere, and starts from the top whenever the actor changes.
    /// </summary>
    private void TrackSelection(InterfaceManager manager, Actor actor)
    {
        if (actor != _lastActor)
        {
            _lastActor = actor;
            _lastSelected = manager.SelectedComponent;
            _scrollTarget = 0;
            return;
        }

        var selected = manager.SelectedComponent;
        if (selected == _lastSelected) return;

        _lastSelected = selected;
        _scrollTarget = -1;
        if (selected == null) return;

        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Component != selected || _rows[i].Kind != RowKind.Component) continue;

            _scrollTarget = i;
            return;
        }
    }

    private void DrawTransport(Actor actor)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 3f));

        if (IconButton("##rewind", RewindIcon, false, "Back to the start"))
        {
            Seek(0f);
        }

        var playing = IsPlaying;
        ImGui.SameLine();
        if (IconButton("##play", playing ? PauseIcon : PlayIcon, playing, playing ? "Pause" : "Play"))
        {
            foreach (var clock in _clocks)
            {
                clock.IsPlayingAnimation = !playing;
            }
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(_textColor, $"{Playhead:0.00}");
        ImGui.SameLine(0f, 3f);
        ImGui.TextColored(_dimColor, $"/ {_duration:0.00}s");

        var rate = _clocks.Count > 0 ? _clocks[0].Animation?.PlayRate ?? 1f : 1f;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1f, 1f, 1f, 0.04f));
        if (ImGui.DragFloat("##Speed", ref rate, 0.01f, 0.05f, 8f, "%.2fx"))
        {
            foreach (var clock in _clocks)
            {
                // a driven prop keeps whatever rate it was given: retiming the performance from here
                // would silently wipe it, and the inspector is where a prop's own rate is set
                if (clock.Animation is { } animation && clock.Relation is not SkeletalMeshComponent) animation.PlayRate = rate;
            }
        }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Playback speed");

        // whose performance this is, since the window no longer lists every actor
        ImGui.SameLine();
        var nameWidth = ImGui.CalcTextSize(actor.Name).X;
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rightEdge - nameWidth));
        ImGui.TextColored(_dimColor, actor.Name);

        ImGui.PopStyleVar(2);
    }

    /// <summary>
    /// The actor's own position, which is the first clock added and so the first component listed.
    /// Everything it drives keeps its own, drawn per row.
    /// </summary>
    private float Playhead => _clocks.Count > 0 ? _clocks[0].AnimationTime : 0f;

    private bool IsPlaying
    {
        get
        {
            foreach (var clock in _clocks)
            {
                if (clock.IsPlayingAnimation) return true;
            }

            return false;
        }
    }

    private void Seek(float time)
    {
        foreach (var clock in _clocks)
        {
            clock.AnimationTime = time;
        }
    }

    /// <summary>Screen x of a point in time, clamped to the track.</summary>
    private float TimeToX(float time) => _trackX + Math.Clamp(time / _duration, 0f, 1f) * _trackWidth;

    /// <summary>
    /// Measures the track once for the whole frame. The ruler is drawn in this window while the rows
    /// live in a scrolling child, so anything measured twice drifts by the width of the scrollbar.
    /// </summary>
    private void UpdateLayout()
    {
        _rowHeight = ImGui.GetTextLineHeight() + RowPadY * 2f;
        _rowWidth = ImGui.GetContentRegionAvail().X;

        if (_rows.Count * _rowHeight > ImGui.GetContentRegionAvail().Y - RulerHeight)
        {
            _rowWidth -= ImGui.GetStyle().ScrollbarSize;
        }

        _trackX = ImGui.GetCursorScreenPos().X + NameWidth;
        _trackWidth = MathF.Max(1f, _rowWidth - NameWidth);
    }

    private void DrawRuler()
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        // dragging anywhere along the ruler scrubs the clock
        ImGui.SetCursorScreenPos(origin with { X = _trackX });
        ImGui.InvisibleButton("##Scrub", new Vector2(_trackWidth, RulerHeight));
        if (ImGui.IsItemActive())
        {
            var ratio = (ImGui.GetMousePos().X - _trackX) / _trackWidth;
            Seek(Math.Clamp(ratio, 0f, 1f) * _duration);
        }

        var step = _tickSteps[^1];
        foreach (var candidate in _tickSteps)
        {
            if (candidate / _duration * _trackWidth < MinTickGap) continue;

            step = candidate;
            break;
        }

        var bottom = origin.Y + RulerHeight;
        drawList.AddLine(new Vector2(origin.X, bottom), new Vector2(origin.X + _rowWidth, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));

        for (var t = 0f; t <= _duration + 0.0001f; t += step)
        {
            var x = MathF.Round(TimeToX(t));
            drawList.AddLine(new Vector2(x, bottom - 4f), new Vector2(x, bottom), ImGui.GetColorU32(_dimColor));
            drawList.AddText(new Vector2(x + 3f, origin.Y), ImGui.GetColorU32(_dimColor), $"{t:0.##}s");
        }

        // the playhead handle lives in the ruler, the line itself is drawn over the rows
        var headX = MathF.Round(TimeToX(Playhead));
        var color = ImGui.GetColorU32(_ownPalette.Head);
        drawList.AddLine(new Vector2(headX, origin.Y), new Vector2(headX, bottom), color);
        drawList.AddTriangleFilled(
            new Vector2(headX - 4f, bottom - 5f),
            new Vector2(headX + 4f, bottom - 5f),
            new Vector2(headX, bottom),
            color);

        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
    }

    private void DrawRows(InterfaceManager manager)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        // no padding, or the rows start further right than the ruler they have to line up with
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.22f));
        var visible = ImGui.BeginChild("##TimelineRows", Vector2.Zero);
        ImGui.PopStyleColor();
        // the child has taken its padding: left pushed, it would strip every tooltip a row raises too
        ImGui.PopStyleVar();

        if (!visible)
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();

        if (_scrollTarget >= 0)
        {
            ImGui.SetScrollY(_scrollTarget * _rowHeight);
            _scrollTarget = -1;
        }

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_rows.Count, _rowHeight);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    DrawRow(manager, _rows[i], i);
                }
            }

            clipper.End();
            clipper.Destroy();
        }

        // one playhead over every row, matching the ruler handle, and the edge of the gutter
        var top = ImGui.GetWindowPos().Y;
        var bottom = top + ImGui.GetWindowHeight();
        drawList.AddLine(new Vector2(_trackX, top), new Vector2(_trackX, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));

        var headX = MathF.Round(TimeToX(Playhead));
        drawList.AddLine(new Vector2(headX, top), new Vector2(headX, bottom), ImGui.GetColorU32(_ownPalette.Head with { W = 0.55f }));

        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    private void DrawRow(InterfaceManager manager, Row row, int index)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        if (ImGui.InvisibleButton($"##row{index}", new Vector2(_rowWidth, _rowHeight)))
        {
            OnRowClicked(manager, row, origin);
        }

        var hovered = ImGui.IsItemHovered();
        var selected = row.Kind == RowKind.Component && manager.SelectedComponent == row.Component;
        if (selected || hovered)
        {
            drawList.AddRectFilled(origin, new Vector2(origin.X + _rowWidth, origin.Y + _rowHeight), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, selected ? 0.10f : 0.04f)));
        }

        DrawRowLabel(drawList, row, origin);

        drawList.PushClipRect(new Vector2(_trackX, origin.Y), new Vector2(origin.X + _rowWidth, origin.Y + _rowHeight), true);
        DrawRowTrack(drawList, row, origin);
        drawList.PopClipRect();

        if (!hovered) return;

        if (ImGui.GetMousePos().X >= _trackX)
        {
            DrawTrackTooltip(row);
        }
        else
        {
            // the gutter elides, so the full name has to be reachable somehow
            ImGui.SetTooltip(row.BarLabel.Length > 0 && row.BarLabel != row.Label ? $"{row.Label}\n{row.BarLabel}" : row.Label);
        }
    }

    /// <summary>
    /// Names the sequence under the cursor. Notifies get no tooltip: each one already carries its
    /// name in the gutter, and the long notify states span the whole montage, so hit testing them
    /// only ever reported whichever happened to come first.
    /// </summary>
    private void DrawTrackTooltip(Row row)
    {
        if (row.Kind is RowKind.NotifyGroup or RowKind.Notifies || row.Animation is not { } animation) return;

        var time = (ImGui.GetMousePos().X - _trackX) / _trackWidth * _duration;
        foreach (var sequence in animation.Sequences)
        {
            if (time < sequence.StartTime || time >= sequence.EndTime) continue;

            ImGui.SetTooltip($"{sequence.Name}\n{sequence.StartTime:0.00}s -> {sequence.EndTime:0.00}s  {sequence.FrameCount} frames @ {sequence.FrameRate:0.#} fps");
            return;
        }
    }

    private void DrawRowLabel(ImDrawListPtr drawList, Row row, Vector2 origin)
    {
        var textY = origin.Y + RowPadY;
        var x = origin.X + 4f + row.Depth * IndentWidth;

        drawList.PushClipRect(origin, new Vector2(_trackX - 4f, origin.Y + _rowHeight), true);

        if (row.Expandable)
        {
            drawList.AddText(new Vector2(x, textY), ImGui.GetColorU32(_dimColor), row.Expanded ? ExpandedIcon : CollapsedIcon);
        }
        x += CaretWidth;

        var color = row.Kind == RowKind.Component
            ? row.Skeletal is { IsPlayingAnimation: false } ? _dimColor : _textColor
            : _dimColor;

        // the detail owns the right end of the gutter, so the name gets whatever is left and no more
        var detailWidth = row.Detail.Length > 0 ? ImGui.CalcTextSize(row.Detail).X : 0f;
        if (detailWidth > 0f)
        {
            drawList.AddText(new Vector2(_trackX - 8f - detailWidth, textY), ImGui.GetColorU32(_dimColor), row.Detail);
            detailWidth += 8f;
        }

        DrawElided(drawList, new Vector2(x, textY), _trackX - 8f - detailWidth - x, ImGui.GetColorU32(color), row.Label);

        drawList.PopClipRect();
    }

    /// <summary>
    /// Draws text cut to a width with a trailing ellipsis. Asset names are far longer than any
    /// gutter, and silently overlapping the next column is worse than losing the tail.
    /// </summary>
    private static void DrawElided(ImDrawListPtr drawList, Vector2 position, float maxWidth, uint color, string text)
    {
        if (maxWidth <= 0f || text.Length == 0) return;

        if (ImGui.CalcTextSize(text).X <= maxWidth)
        {
            drawList.AddText(position, color, text);
            return;
        }

        const string ellipsis = "...";
        var budget = maxWidth - ImGui.CalcTextSize(ellipsis).X;
        if (budget <= 0f) return;

        // binary search so a long name costs a handful of measures rather than one per character
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..middle]).X <= budget) low = middle;
            else high = middle - 1;
        }

        if (low > 0) drawList.AddText(position, color, text[..low] + ellipsis);
    }

    private void DrawRowTrack(ImDrawListPtr drawList, Row row, Vector2 origin)
    {
        var top = origin.Y + BarInset;
        var bottom = origin.Y + _rowHeight - BarInset;
        var palette = row.Palette;
        var head = Playhead;

        // where this component is inside its own animation right now, which a driven prop running at
        // its own rate or holding at its own end will have drifted away from the actor's clock
        var local = row.Skeletal is { Animation.Duration: > 0f } skeletal ? skeletal.AnimationTime : head;

        switch (row.Kind)
        {
            case RowKind.Component when row.Animation is { } animation:
            {
                drawList.AddRectFilled(new Vector2(TimeToX(0f), top), new Vector2(TimeToX(animation.Duration), bottom), ImGui.GetColorU32(_trackColor));

                for (var i = 0; i < animation.Sequences.Length; i++)
                {
                    DrawSequenceBar(drawList, animation.Sequences[i], local, i % 2 == 0 ? palette.Bar : palette.BarAlt, palette, top, bottom);
                }

                // without sequences the bar is only the faint track fill, which cannot carry white
                DrawBarLabel(drawList, row, TimeToX(0f), TimeToX(animation.Duration), origin.Y, animation.Sequences.Length > 0 ? _textColor : _dimColor);

                break;
            }
            case RowKind.Component:
            {
                // driven but not animated: it is simply attached for the whole of its driver's animation
                drawList.AddRectFilled(new Vector2(TimeToX(0f), top), new Vector2(TimeToX(_duration), bottom), ImGui.GetColorU32(palette.Bar with { W = 0.55f }));
                DrawBarLabel(drawList, row, TimeToX(0f), TimeToX(_duration), origin.Y, _dimColor);
                break;
            }
            case RowKind.Sequence when row.Animation is { } animation && row.Index < animation.Sequences.Length:
            {
                var sequence = animation.Sequences[row.Index];
                DrawSequenceBar(drawList, sequence, local, palette.Bar, palette, top, bottom);
                DrawBarLabel(drawList, row, TimeToX(sequence.StartTime), TimeToX(sequence.EndTime), origin.Y, _textColor);
                break;
            }
            case RowKind.NotifyGroup or RowKind.Notifies when row.Animation is { } animation:
            {
                var middle = (top + bottom) * 0.5f;
                drawList.AddLine(new Vector2(TimeToX(0f), middle), new Vector2(TimeToX(animation.Duration), middle), ImGui.GetColorU32(_trackColor));
                foreach (var notify in animation.Notifies)
                {
                    // the group carries every lane at once, so the shape survives being collapsed
                    if (row.Kind == RowKind.Notifies && notify.TrackIndex != row.Index) continue;

                    DrawNotify(drawList, notify, top, bottom);
                }
                break;
            }
        }

        // this row's clock, in its own colour, and only once it has left the actor's playhead behind
        var x = MathF.Round(TimeToX(local));
        if (MathF.Abs(local - head) > 0.001f)
        {
            drawList.AddLine(new Vector2(x, top), new Vector2(x, bottom), ImGui.GetColorU32(palette.Head));
        }

        if (row.Kind == RowKind.Component && row.Animation is { } playing)
        {
            DrawRate(drawList, playing.PlayRate, x, origin.Y);
        }
    }

    /// <summary>
    /// One sequence span, lifted a step while the clock is inside it. The lift stops well short of
    /// the accent so the fill still carries a white label.
    /// </summary>
    private void DrawSequenceBar(ImDrawListPtr drawList, SequenceDescriptor sequence, float local, Vector4 fill, Palette palette, float top, float bottom)
    {
        var active = local >= sequence.StartTime && local < sequence.EndTime;

        drawList.AddRectFilled(
            new Vector2(TimeToX(sequence.StartTime), top),
            new Vector2(TimeToX(sequence.EndTime) - 1f, bottom),
            ImGui.GetColorU32(active ? palette.Active : fill));
    }

    /// <summary>
    /// The asset name rides on its own bar, the way a clip is labelled in any editing timeline.
    /// It is the only column wide enough to hold one.
    /// </summary>
    private void DrawBarLabel(ImDrawListPtr drawList, Row row, float left, float right, float rowY, Vector4 color)
    {
        if (row.BarLabel.Length == 0) return;

        drawList.PushClipRect(new Vector2(left, rowY), new Vector2(right, rowY + _rowHeight), true);
        DrawElided(drawList, new Vector2(left + 5f, rowY + RowPadY), right - left - 8f, ImGui.GetColorU32(color), row.BarLabel);
        drawList.PopClipRect();
    }

    /// <summary>
    /// How fast the row is playing, riding its clock tick since the tick is the thing moving at that
    /// rate. Only worth the ink when it is not 1x, which since props got their own clocks no longer
    /// means the whole actor was retimed.
    /// </summary>
    private void DrawRate(ImDrawListPtr drawList, float rate, float tickX, float rowY)
    {
        if (Math.Abs(rate - 1f) <= 0.001f) return;

        // semibold at a smaller size, the same trick the hardware band uses to stay legible when it
        // has to sit on top of something else
        var text = $"{rate:0.##}x";
        var font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        var fontSize = ImGui.GetFontSize() * RateFontScale;
        var width = font.CalcTextSizeA(fontSize, float.MaxValue, 0f, text).X;

        // reads on the near side of the tick rather than run off the end of the track
        var right = _trackX + _trackWidth;
        var x = tickX + 4f + width <= right ? tickX + 4f : tickX - 4f - width;

        drawList.AddText(font, fontSize, new Vector2(x, rowY + RowPadY + 1f), ImGui.GetColorU32(_rateColor), text);
    }

    private void DrawNotify(ImDrawListPtr drawList, NotifyDescriptor notify, float top, float bottom)
    {
        var color = ImGui.GetColorU32(_notifyColor);
        var start = TimeToX(notify.TriggerTime);

        if (notify.IsState)
        {
            drawList.AddRectFilled(new Vector2(start, top), new Vector2(TimeToX(notify.TriggerTime + notify.Duration), bottom), ImGui.GetColorU32(_notifyColor with { W = 0.35f }));
        }

        var middle = (top + bottom) * 0.5f;
        drawList.AddTriangleFilled(new Vector2(start, middle - NotifySize), new Vector2(start + NotifySize, middle), new Vector2(start, middle + NotifySize), color);
        drawList.AddTriangleFilled(new Vector2(start, middle - NotifySize), new Vector2(start - NotifySize, middle), new Vector2(start, middle + NotifySize), color);
    }

    /// <summary>
    /// One hit target per row, split by where along the gutter the click landed: the caret expands,
    /// the toggle plays, the rest selects. Keeping it to a single item is what lets the whole row
    /// highlight on hover.
    /// </summary>
    private void OnRowClicked(InterfaceManager manager, Row row, Vector2 origin)
    {
        var mouseX = ImGui.GetMousePos().X;
        var caretX = origin.X + 4f + row.Depth * IndentWidth;

        if (row.Expandable && mouseX >= caretX && mouseX < caretX + CaretWidth)
        {
            // one set holds what is open and the other what is shut, but either way the click flips it
            var toggled = row.Kind == RowKind.NotifyGroup ? _expandedNotifies : _collapsed;
            if (!toggled.Add(row.Component.Id)) toggled.Remove(row.Component.Id);
            return;
        }

        var playX = caretX + CaretWidth;
        if (row.Kind == RowKind.Component && row.Skeletal is { Animation: not null } skeletal && mouseX >= playX && mouseX < playX + CaretWidth)
        {
            skeletal.IsPlayingAnimation = !skeletal.IsPlayingAnimation;
            return;
        }

        // selecting from the timeline itself must not yank the view around, so the selection is
        // marked as already seen
        manager.SelectComponent(row.Component);
        _lastSelected = row.Component;
    }

    /// <summary>
    /// Frameless square toggle, the default button chrome would drown a strip this small.
    /// </summary>
    private static bool IconButton(string id, string label, bool active, string tooltip)
    {
        var size = new Vector2(ImGui.GetFrameHeight());
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        if (active || hovered)
        {
            drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(_textColor with { W = active ? 0.18f : 0.08f }));
        }

        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(origin + (size - textSize) * 0.5f, ImGui.GetColorU32(active || hovered ? _textColor : _textColor with { W = 0.45f }), label);

        if (hovered) ImGui.SetTooltip(tooltip);

        return clicked;
    }
}
