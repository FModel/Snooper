using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Systems;

namespace Editor.Widgets.Timeline;

/// <summary>
/// Playback view of the selected actor, tied to <see cref="SkinnedMeshRenderSystem"/>: what it is
/// playing, when each sequence, notify and curve lands, and what those animations drive through the
/// components attached to them. The transport drives that actor's clocks only, so pausing or seeking
/// one performance leaves every other actor in the scene running.
/// </summary>
public class TimelineWidget
{
    private readonly TimelineRowBuilder _builder = new();
    private readonly TimelineLayout _layout = new();

    private Actor? _lastActor;
    private ActorComponent? _lastSelected;
    private int _scrollTarget = -1;

    public void Draw(InterfaceManager manager)
    {
        if (!ImGui.Begin(TimelineStyle.Title))
        {
            ImGui.End();
            return;
        }

        var system = manager.GetSystem<SkinnedMeshRenderSystem>();
        if (system == null)
        {
            TimelineEmptyState.Draw("Nothing to play", "This scene has no skinned meshes.");
            ImGui.End();
            return;
        }

        var actor = manager.SelectedActor ?? manager.SelectedComponent?.Actor;
        if (actor == null)
        {
            TimelineEmptyState.Draw("No actor selected", "Pick one in the hierarchy or the viewport.");
            ImGui.End();
            return;
        }

        _builder.Refresh(actor);
        if (_builder.Rows.Count == 0)
        {
            TimelineEmptyState.Draw("Nothing animated", $"{actor.Name} has no component playing an animation.");
            ImGui.End();
            return;
        }

        TrackSelection(manager, actor);
        DrawTransport(actor);

        // the rows measure the track, so the ruler that has to line up with it is drawn afterwards
        // and only its strip is set aside here
        var origin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(origin with { Y = origin.Y + TimelineStyle.RulerHeight });

        DrawRows(manager);
        DrawRuler(origin.Y);

        ImGui.End();
    }

    /// <summary>
    /// The actor's own position, which is the performance's clock and so the first one listed. Every mesh
    /// taking part reads it, and only what the performance drives keeps a clock of its own.
    /// </summary>
    private float Playhead => _builder.Clocks.Count > 0 ? _builder.Clocks[0].Time : 0f;

    private bool IsPlaying
    {
        get
        {
            foreach (var clock in _builder.Clocks)
            {
                if (clock.IsPlaying) return true;
            }

            return false;
        }
    }

    private void Seek(float time)
    {
        foreach (var clock in _builder.Clocks)
        {
            clock.Seek(time);
        }
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

        for (var i = 0; i < _builder.Rows.Count; i++)
        {
            if (_builder.Rows[i].Component != selected || _builder.Rows[i].Kind != TimelineRowKind.Component) continue;

            _scrollTarget = i;
            return;
        }
    }

    private void DrawTransport(Actor actor)
    {
        if (TimelineStyle.IconButton("##rewind", TimelineStyle.RewindIcon, false, "Back to the start"))
        {
            Seek(0f);
        }

        var playing = IsPlaying;
        ImGui.SameLine();
        if (TimelineStyle.IconButton("##play", playing ? TimelineStyle.PauseIcon : TimelineStyle.PlayIcon, playing, playing ? "Pause" : "Play"))
        {
            foreach (var clock in _builder.Clocks)
            {
                clock.IsPlaying = !playing;
            }
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(TimelineStyle.Text, $"{Playhead:0.00}");
        ImGui.SameLine(0f, 3f);
        ImGui.TextColored(TimelineStyle.Dim, $"/ {_builder.Duration:0.00}s");

        var rate = _builder.Clocks.Count > 0 ? _builder.Clocks[0].PlayRate : 1f;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        if (ImGui.DragFloat("##Speed", ref rate, 0.01f, 0.05f, 8f, "%.2fx"))
        {
            foreach (var clock in _builder.Clocks)
            {
                // a driven prop keeps whatever rate it was given: retiming the performance from here
                // would silently wipe it, and the inspector is where a prop's own rate is set
                if (!clock.IsDriven) clock.PlayRate = rate;
            }
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Playback speed");

        // whose performance this is, since the window no longer lists every actor
        ImGui.SameLine();
        var nameWidth = ImGui.CalcTextSize(actor.Name).X;
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), rightEdge - nameWidth));
        ImGui.TextColored(TimelineStyle.Dim, actor.Name);
    }

    private void DrawRows(InterfaceManager manager)
    {
        var visible = ImGui.BeginChild("##TimelineRows", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground);
        if (!visible)
        {
            ImGui.EndChild();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        _layout.Measure(_builder.Duration);

        var pitch = ImGui.GetFrameHeightWithSpacing();
        if (_scrollTarget >= 0)
        {
            ImGui.SetScrollY(_scrollTarget * pitch);
            _scrollTarget = -1;
        }

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_builder.Rows.Count, pitch);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    DrawRow(manager, _builder.Rows[i]);
                }
            }

            clipper.End();
            clipper.Destroy();
        }

        // one playhead over every row, matching the ruler handle, and the edge of the gutter
        var top = ImGui.GetWindowPos().Y;
        var bottom = top + ImGui.GetWindowHeight();
        drawList.AddLine(new Vector2(_layout.TrackX, top), new Vector2(_layout.TrackX, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));

        var headX = MathF.Round(_layout.TimeToX(Playhead));
        drawList.AddLine(new Vector2(headX, top), new Vector2(headX, bottom), ImGui.GetColorU32(TimelineStyle.Own.Head with { W = 0.55f }));

        ImGui.EndChild();
    }

    /// <summary>
    /// The scale the rows are read against, drawn over the track they measured. Dragging it scrubs the
    /// whole performance, which is why it takes the width of the track and not of the window.
    /// </summary>
    private void DrawRuler(float top)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bottom = top + TimelineStyle.RulerHeight;

        ImGui.SetCursorScreenPos(new Vector2(_layout.TrackX, top));
        ImGui.InvisibleButton("##Scrub", new Vector2(_layout.TrackWidth, TimelineStyle.RulerHeight));
        if (ImGui.IsItemActive())
        {
            var ratio = (ImGui.GetMousePos().X - _layout.TrackX) / _layout.TrackWidth;
            Seek(Math.Clamp(ratio, 0f, 1f) * _builder.Duration);
        }

        var step = TimelineStyle.TickSteps[^1];
        foreach (var candidate in TimelineStyle.TickSteps)
        {
            if (candidate / _builder.Duration * _layout.TrackWidth < TimelineStyle.MinTickGap) continue;

            step = candidate;
            break;
        }

        var left = _layout.TrackX - TimelineStyle.NameWidth;
        drawList.AddLine(new Vector2(left, bottom), new Vector2(left + _layout.RowWidth, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));

        for (var t = 0f; t <= _builder.Duration + 0.0001f; t += step)
        {
            var x = MathF.Round(_layout.TimeToX(t));
            drawList.AddLine(new Vector2(x, bottom - 4f), new Vector2(x, bottom), ImGui.GetColorU32(TimelineStyle.Dim));
            drawList.AddText(new Vector2(x + 3f, top), ImGui.GetColorU32(TimelineStyle.Dim), $"{t:0.##}s");
        }

        // the playhead handle lives in the ruler, the line itself is drawn over the rows
        var headX = MathF.Round(_layout.TimeToX(Playhead));
        var color = ImGui.GetColorU32(TimelineStyle.Own.Head);
        drawList.AddLine(new Vector2(headX, top), new Vector2(headX, bottom), color);
        drawList.AddTriangleFilled(
            new Vector2(headX - 4f, bottom - 5f),
            new Vector2(headX + 4f, bottom - 5f),
            new Vector2(headX, bottom),
            color);
    }

    /// <summary>
    /// One row, hung off a real tree node so it carries an arrow, a highlight and a context menu the
    /// way the hierarchy and inspector rows do. Only the gutter text is drawn by hand, because it has
    /// to elide and share the column with a right-aligned detail, and the track is all draw list work
    /// over the top: the node spans the full width, so the whole row is one hit target.
    /// </summary>
    private void DrawRow(InterfaceManager manager, TimelineRow row)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var indentX = origin.X + row.Depth * TimelineStyle.IndentWidth;

        ImGui.PushID(row.Component.Id);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + row.Depth * TimelineStyle.IndentWidth);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                    ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding |
                    ImGuiTreeNodeFlags.NoTreePushOnOpen; // the depth is drawn by hand, so nothing to pop
        if (row.Selectable && manager.SelectedComponent == row.Component) flags |= ImGuiTreeNodeFlags.Selected;
        if (!row.Expandable) flags |= ImGuiTreeNodeFlags.Leaf;
        else ImGui.SetNextItemOpen(row.Expanded, ImGuiCond.Always);

        // a component owns several rows, so the id has to say which one this is
        var open = ImGui.TreeNodeEx($"##{row.Kind}{row.Index}", flags, string.Empty);
        var hovered = ImGui.IsItemHovered();
        var toggled = ImGui.IsItemToggledOpen();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        // the menu hangs off the last item submitted, so it has to be raised before the toggle is
        if (row.Selectable) DrawRowContextMenu(row);

        // the toggle overlaps the node, so it has to be asked whether it took the click first
        var consumed = DrawPlayToggle(row, origin);

        if (toggled)
        {
            _builder.SetExpanded(row, open);
        }
        else if (clicked && !consumed && row.Selectable)
        {
            // selecting from the timeline itself must not yank the view around, so the selection is
            // marked as already seen
            manager.SelectComponent(row.Component);
            _lastSelected = row.Component;
        }

        ImGui.PopID();

        // where this component is inside its own animation right now, which a driven prop running at
        // its own rate or holding at its own end will have drifted away from the actor's clock
        var head = Playhead;
        var local = row.Skeletal?.Playback is { Duration: > 0f } clock ? clock.Time : head;

        // the one thing a row cannot know until the clock moves, measured here so the gutter and the
        // plot read the same number off one evaluation
        var value = row.HasReadout && row.Animation is { } animation ? TimelineCurves.Value(animation, row.Label, local) : null;

        DrawRowLabel(drawList, row, origin, indentX, value);

        drawList.PushClipRect(new Vector2(_layout.TrackX, origin.Y), new Vector2(origin.X + _layout.RowWidth, origin.Y + _layout.RowHeight), true);
        TimelineTrack.Draw(drawList, _layout, row, origin, head, local, value);
        drawList.PopClipRect();

        if (!hovered) return;

        if (ImGui.GetMousePos().X >= _layout.TrackX)
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
    /// The gutter text, drawn over the node the way the node would have drawn its own label: past the
    /// arrow it reserved, on the baseline its frame padding puts text on.
    /// </summary>
    private void DrawRowLabel(ImDrawListPtr drawList, TimelineRow row, Vector2 origin, float indentX, float? value)
    {
        var textY = origin.Y + _layout.TextPadY;
        var x = indentX + _layout.ArrowWidth;
        var detail = row.HasReadout ? value is { } under ? $"{under:0.##}" : string.Empty : row.Detail;

        drawList.PushClipRect(origin, new Vector2(_layout.TrackX - 4f, origin.Y + _layout.RowHeight), true);

        var color = row.Kind == TimelineRowKind.Component
            ? row.Skeletal?.Playback is { IsPlaying: false } ? TimelineStyle.Dim : TimelineStyle.Text
            : TimelineStyle.Dim;

        // the detail owns the right end of the gutter, so the name gets whatever is left and no more.
        // A component row carries no detail, which is what leaves that end free for its play toggle
        var reserved = detail.Length > 0 ? ImGui.CalcTextSize(detail).X : 0f;
        if (reserved > 0f)
        {
            drawList.AddText(new Vector2(_layout.TrackX - 8f - reserved, textY), ImGui.GetColorU32(TimelineStyle.Dim), detail);
            reserved += 8f;
        }
        else if (row.HasToggle) reserved = _layout.ArrowWidth + 8f;

        drawList.AddText(new Vector2(x, textY), ImGui.GetColorU32(color), row.FitLabel(_layout.TrackX - 8f - reserved - x));

        drawList.PopClipRect();
    }

    /// <summary>
    /// The row's own play toggle, at the far end of the gutter where the hierarchy keeps its eye. It
    /// overlaps the node rather than sitting inside it, so it reports back whether it ate the click.
    /// </summary>
    private bool DrawPlayToggle(TimelineRow row, Vector2 origin)
    {
        if (!row.HasToggle) return false;

        if (row.Skeletal?.Playback is not { } clock) return false;

        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(_layout.TrackX - _layout.ArrowWidth - 4f, origin.Y));

        // no tooltip: the glyph says what it does, and the row already raises one of its own
        if (!TimelineStyle.IconButton("##Toggle", clock.IsPlaying ? TimelineStyle.PauseIcon : TimelineStyle.PlayIcon, false, string.Empty, new Vector2(_layout.ArrowWidth, _layout.RowHeight)))
        {
            return ImGui.IsItemHovered();
        }

        // every mesh bound to this performance follows, which is the point of it being one clock
        clock.IsPlaying = !clock.IsPlaying;
        return true;
    }

    private static void DrawRowContextMenu(TimelineRow row)
    {
        if (!ImGui.BeginPopupContextItem()) return;

        ImGui.TextDisabled(row.Label);
        ImGui.Separator();

        if (ImGui.MenuItem($"{TimelineStyle.ExportIcon}  Export"))
        {
            // ExportModal.Instance.Export(actor, "./exports_v2", new ExportOptions());
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
        if (ImGui.MenuItem($"{Settings.TrashIcon}  Delete"))
        {
            // actor.Parent?.Children.Remove(actor);
            // _dirty = true;
        }
        ImGui.PopStyleColor();

        ImGui.EndPopup();
    }

    /// <summary>
    /// Names the section or the segment under the cursor, or reads out the curve there, since a plot
    /// normalised to its own range carries no scale of its own. Notifies get no tooltip: each one carries
    /// its name in the gutter, and the long notify states span the whole montage, so hit testing them
    /// only ever reported whichever happened to come first.
    /// </summary>
    private void DrawTrackTooltip(TimelineRow row)
    {
        if (row.Animation is not { } animation) return;

        var time = _layout.XToTime(ImGui.GetMousePos().X);

        if (row.Kind == TimelineRowKind.Curve)
        {
            if (TimelineCurves.Value(animation, row.Label, time) is { } value) ImGui.SetTooltip($"{row.Label}\n{value:0.###} at {time:0.00}s");
            return;
        }

        // the component row carries the montage's sections, drawn cut to their own part rather than
        // elided, so the name one of them could not fit and where it hands over next are read here
        if (row.Kind == TimelineRowKind.Component)
        {
            var sections = row.Montage?.Sections ?? [];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (!section.IsActiveAt(time)) continue;

                var next = section.NextIndex < 0 ? "ends"
                    : section.NextIndex == i ? $"{Settings.LoopIcon} {Settings.InfinityIcon}"
                    : sections[section.NextIndex].Name;

                ImGui.SetTooltip($"{animation.Name}\n{section.Name}  {section.StartTime:0.00}s -> {section.EndTime:0.00}s  ({section.Duration:0.00}s)\nthen {next}");
                return;
            }

            // sectionless, so the row is showing the animation itself rather than any part of it
            ImGui.SetTooltip($"{animation.Name}\n{animation.Duration:0.00}s");
            return;
        }

        if (row.Kind != TimelineRowKind.Slot) return;

        foreach (var segment in row.Segments)
        {
            if (!segment.IsActiveAt(time)) continue;

            var loop = segment.LoopCount > 1 ? $"  {Settings.LoopIcon} {segment.LoopCount}" : string.Empty;
            ImGui.SetTooltip($"{segment.Sequence.Name}\n{segment.StartPos:0.00}s -> {segment.EndPos:0.00}s{loop}\n{segment.Sequence.FrameCount} frames @ {segment.Sequence.FrameRate:0.#} fps");
            return;
        }
    }
}
