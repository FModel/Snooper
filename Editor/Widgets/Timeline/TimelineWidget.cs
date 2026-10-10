using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Hosting;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Descriptors.Animations;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets.Timeline;

/// <summary>
/// Playback view of the selected actor, one block per animation it plays, all on one scale: how each
/// is cut and keyed, who plays it, who follows them, and the clips their props play. The transport
/// drives that actor's clocks only, so pausing or seeking one performance leaves every other actor running.
/// </summary>
public class TimelineWidget : PanelWidget
{
    public override string PanelTitle => TimelineStyle.Title;
    public override PanelGroup Group => PanelGroup.Editor;

    private readonly TimelineRows _rows = new();
    private readonly TimelineLayout _layout = new();

    private Actor? _lastActor;
    private ActorComponent? _lastSelected;
    private int _scrollTarget = -1;

    protected override void DrawContents(EditorManager editor)
    {
        var actor = editor.SelectedActor ?? editor.SelectedComponent?.Actor;
        if (actor == null)
        {
            TimelineEmptyState.Draw("No actor selected", "Pick one in the hierarchy or the viewport.");
            return;
        }

        _rows.Refresh(actor);
        if (_rows.Blocks.Count == 0)
        {
            TimelineEmptyState.Draw("Nothing animated", $"{actor.Name} plays no animation, set one on a skeletal mesh.");
            return;
        }

        TrackSelection(editor, actor);

        // the rows measure the track, so the ruler that has to line up with it is drawn afterwards
        // and only its strip is set aside here
        var origin = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(origin with { Y = origin.Y + TimelineStyle.RulerHeight });

        DrawRows(editor);
        DrawRuler(origin.Y);
    }

    /// <summary>The actor's playhead: the first clock of its first animation.</summary>
    private float Playhead => _rows.Blocks[0].Time;

    private bool IsPlaying
    {
        get
        {
            foreach (var block in _rows.Blocks)
            {
                foreach (var clock in block.Clocks)
                {
                    if (clock.IsPlaying) return true;
                }
            }

            return false;
        }
    }

    private void SeekAll(float time)
    {
        foreach (var block in _rows.Blocks)
        {
            Seek(block, time);
        }
    }

    private static void Seek(TimelineBlock block, float time)
    {
        foreach (var clock in block.Clocks)
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

        for (var i = 0; i < _rows.Rows.Count; i++)
        {
            if (_rows.Rows[i].Component != selected) continue;

            _scrollTarget = i;
            return;
        }
    }

    /// <summary>
    /// The strip above the rows: the transport in the gutter, the one scale on the track. Dragging the
    /// scale scrubs every clock of the actor, which is why it takes the width of the track and not of
    /// the window.
    /// </summary>
    private void DrawRuler(float top)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bottom = top + TimelineStyle.RulerHeight;

        ImGui.SetCursorScreenPos(new Vector2(_layout.TrackX - TimelineStyle.NameWidth, top));
        if (TimelineStyle.IconButton("##rewind", TimelineStyle.RewindIcon, false, "Back to the start")) SeekAll(0f);

        var playing = IsPlaying;
        ImGui.SameLine(0f, ImGui.GetStyle().ItemInnerSpacing.X);
        if (TimelineStyle.IconButton("##play", playing ? TimelineStyle.PauseIcon : TimelineStyle.PlayIcon, playing, playing ? "Pause" : "Play"))
        {
            foreach (var block in _rows.Blocks)
            {
                foreach (var clock in block.Clocks)
                {
                    clock.IsPlaying = !playing;
                }
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(_layout.TrackX, top));
        ImGui.InvisibleButton("##Scrub", new Vector2(_layout.TrackWidth, TimelineStyle.RulerHeight));
        if (ImGui.IsItemActive())
        {
            var ratio = (ImGui.GetMousePos().X - _layout.TrackX) / _layout.TrackWidth;
            SeekAll(Math.Clamp(ratio, 0f, 1f) * _layout.Duration);
        }

        TimelineTrack.DrawRuler(drawList, _layout, top, bottom, Playhead);
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
        _layout.Measure(_rows.Duration);

        var pitch = ImGui.GetFrameHeightWithSpacing();
        if (_scrollTarget >= 0)
        {
            ImGui.SetScrollY(_scrollTarget * pitch);
            _scrollTarget = -1;
        }

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_rows.Rows.Count, pitch);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    DrawRow(manager, _rows.Rows[i]);
                }
            }

            clipper.End();
            clipper.Destroy();
        }

        // the actor's playhead over every row, matching the ruler handle, and the edge of the gutter
        var top = ImGui.GetWindowPos().Y;
        var bottom = top + ImGui.GetWindowHeight();
        drawList.AddLine(new Vector2(_layout.TrackX, top), new Vector2(_layout.TrackX, bottom), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));

        var headX = MathF.Round(_layout.TimeToX(Playhead));
        drawList.AddLine(new Vector2(headX, top), new Vector2(headX, bottom), ImGui.GetColorU32(TimelineStyle.Head with { W = 0.55f }));

        ImGui.EndChild();
    }

    /// <summary>
    /// One row, hung off a real tree node so it carries an arrow, a highlight and a context menu the
    /// way the hierarchy and inspector rows do. Only the gutter text is drawn by hand, because it has
    /// to elide and share the column with a right-aligned detail, and the track is all draw list work
    /// over the top: the node spans the full width, so the whole row is one hit target, and holding it
    /// over the track scrubs whatever clock the row owns.
    /// </summary>
    private void DrawRow(InterfaceManager manager, TimelineRow row)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var indentX = origin.X + row.Depth * TimelineStyle.IndentWidth;

        // the band under the row, before the node so its own highlight draws over it
        var band = row.Kind switch
        {
            TimelineRowKind.Header => TimelineStyle.HeaderBand,
            TimelineRowKind.Player => TimelineStyle.PlayerBand,
            _ => Vector4.Zero
        };
        if (band.W > 0f) drawList.AddRectFilled(origin, new Vector2(origin.X + _layout.RowWidth, origin.Y + _layout.RowHeight), ImGui.GetColorU32(band));

        ImGui.PushID(row.Id);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + row.Depth * TimelineStyle.IndentWidth);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                    ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding |
                    ImGuiTreeNodeFlags.NoTreePushOnOpen; // the depth is drawn by hand, so nothing to pop
        if (row.Selectable && manager.SelectedComponent == row.Component) flags |= ImGuiTreeNodeFlags.Selected;
        if (!row.Expandable) flags |= ImGuiTreeNodeFlags.Leaf;
        else ImGui.SetNextItemOpen(row.Expanded, ImGuiCond.Always);

        // an animation or a component owns several rows, so the id has to say which one this is
        var open = ImGui.TreeNodeEx($"##{row.Kind}{row.Index}", flags, string.Empty);
        var hovered = ImGui.IsItemHovered();
        var toggled = ImGui.IsItemToggledOpen();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var held = ImGui.IsItemActive();

        // the menu hangs off the last item submitted, so it has to be raised before anything overlaps it
        switch (row.Kind)
        {
            case TimelineRowKind.Header:
                DrawAnimationContextMenu(manager, row);
                break;
            case TimelineRowKind.Player when row.Component is SkeletalMeshComponent player:
                DrawPlayerContextMenu(player);
                break;
            case TimelineRowKind.Follower when row.Component is SkinnedMeshComponent follower:
                DrawFollowerContextMenu(follower);
                break;
        }

        if (toggled)
        {
            _rows.SetExpanded(row, open);
        }
        else if (held && ImGui.GetMousePos().X >= _layout.TrackX)
        {
            var target = Math.Clamp(_layout.XToTime(ImGui.GetMousePos().X), 0f, row.Animation.Duration);
            switch (row)
            {
                case { Kind: TimelineRowKind.Player, Clock: { } clock }:
                    clock.Seek(target);
                    break;
                case { Kind: TimelineRowKind.Header }:
                    Seek(row.Block, target);
                    break;
            }
        }
        else if (clicked && row.Selectable)
        {
            // selecting from the timeline itself must not yank the view around, so the selection is
            // marked as already seen
            manager.SelectComponent(row.Component);
            _lastSelected = row.Component;
        }

        ImGui.PopID();

        // the one thing a curve row cannot know until the clock moves, measured here so the gutter and
        // the plot read the same number off one evaluation
        var time = row.Block.Time;
        var value = row.Kind == TimelineRowKind.Curve ? TimelineCurves.Value(row.Animation, row.Label, time) : null;

        DrawRowLabel(drawList, row, origin, indentX, time, value);

        drawList.PushClipRect(new Vector2(_layout.TrackX, origin.Y), new Vector2(origin.X + _layout.RowWidth, origin.Y + _layout.RowHeight), true);
        TimelineTrack.Draw(drawList, _layout, row, origin, time, value);
        drawList.PopClipRect();

        if (!hovered) return;

        if (ImGui.GetMousePos().X >= _layout.TrackX) DrawTrackTooltip(row);
        else ImGui.SetTooltip(row.Label); // the gutter elides, so the full name has to be reachable somehow
    }

    /// <summary>
    /// The gutter text, drawn over the node the way the node would have drawn its own label: past the
    /// arrow it reserved, on the baseline its frame padding puts text on. The right end carries the
    /// detail: an animation's time, a group's count, a curve's value.
    /// </summary>
    private void DrawRowLabel(ImDrawListPtr drawList, TimelineRow row, Vector2 origin, float indentX, float time, float? value)
    {
        var textY = origin.Y + _layout.TextPadY;
        var x = indentX + _layout.ArrowWidth;

        var detail = row.Kind switch
        {
            TimelineRowKind.Header => $"{time:0.00} / {row.Animation.Duration:0.00}s",
            TimelineRowKind.Curve => value is { } under ? $"{under:0.##}" : string.Empty,
            _ => row.Detail
        };

        var color = row.Kind switch
        {
            TimelineRowKind.Header => TimelineStyle.Text,
            TimelineRowKind.Player => row.Clock is { IsPlaying: true } ? TimelineStyle.Text : TimelineStyle.Dim,
            _ => TimelineStyle.Dim
        };

        drawList.PushClipRect(origin, new Vector2(_layout.TrackX - 4f, origin.Y + _layout.RowHeight), true);

        var reserved = 0f;
        if (detail.Length > 0)
        {
            reserved = ImGui.CalcTextSize(detail).X;
            drawList.AddText(new Vector2(_layout.TrackX - 8f - reserved, textY), ImGui.GetColorU32(TimelineStyle.Dim), detail);
            reserved += 8f;
        }

        drawList.AddText(new Vector2(x, textY), ImGui.GetColorU32(color), row.FitLabel(_layout.TrackX - 8f - reserved - x));

        drawList.PopClipRect();
    }

    private static void DrawAnimationContextMenu(InterfaceManager manager, TimelineRow row)
    {
        if (!ImGui.BeginPopupContextItem()) return;

        ImGui.TextDisabled(row.Label);
        ImGui.Separator();

        if (ImGui.MenuItem($"{TimelineStyle.ExportIcon}  Export"))
        {
            Bridge.Export(session => session.Add(manager.FileProvider.LoadPackageObject(row.Animation.Path, row.Animation.Name)));
        }

        ImGui.EndPopup();
    }

    /// <summary>
    /// Binding is only ever the explicit pick from the host. Following is the one thing the timeline
    /// does itself: a skinned mesh of the same actor wears this one's pose, bone for bone by name.
    /// </summary>
    private static void DrawPlayerContextMenu(SkeletalMeshComponent player)
    {
        if (!ImGui.BeginPopupContextItem()) return;

        ImGui.TextDisabled(player.Name);
        ImGui.Separator();

        AssetRequestMenu.Animation(player);

        if (player.Actor is { } actor && ImGui.BeginMenu($"{Settings.LinkIcon}  Followers"))
        {
            ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false); // several are usually ticked in a row

            var others = 0;
            for (var i = 0; i < actor.Components.Count; i++)
            {
                if (actor.Components[i] is not SkinnedMeshComponent other || other == player) continue;

                others++;
                var following = other.Leader == player;
                if (ImGui.MenuItem(other.Name, string.Empty, following)) other.Leader = following ? null : player;
            }

            if (others == 0) ImGui.TextDisabled("No other skinned mesh on this actor.");

            ImGui.PopItemFlag();
            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    private static void DrawFollowerContextMenu(SkinnedMeshComponent follower)
    {
        if (!ImGui.BeginPopupContextItem()) return;

        ImGui.TextDisabled(follower.Name);
        ImGui.Separator();

        if (ImGui.MenuItem($"{Settings.LinkIcon}  Stop Following")) follower.Leader = null;

        ImGui.EndPopup();
    }

    /// <summary>
    /// Names the section or the clip under the cursor, or reads out the curve there, since a plot
    /// normalised to its own range carries no scale of its own. Notifies get no tooltip: each one carries
    /// its name in the gutter, and the long notify states span the whole montage, so hit testing them
    /// only ever reported whichever happened to come first.
    /// </summary>
    private void DrawTrackTooltip(TimelineRow row)
    {
        var animation = row.Animation;
        var time = _layout.XToTime(ImGui.GetMousePos().X);

        switch (row.Kind)
        {
            case TimelineRowKind.Curve:
            {
                if (TimelineCurves.Value(animation, row.Label, time) is { } value) ImGui.SetTooltip($"{row.Label}\n{value:0.###} at {time:0.00}s");
                break;
            }
            case TimelineRowKind.Header when animation is MontageDescriptor { Sections.Length: > 0 } montage:
            {
                for (var i = 0; i < montage.Sections.Length; i++)
                {
                    var section = montage.Sections[i];
                    if (!section.IsActiveAt(time)) continue;

                    var next = section.NextIndex < 0 ? "ends"
                        : section.NextIndex == i ? $"{Settings.LoopIcon} {Settings.InfinityIcon}"
                        : montage.Sections[section.NextIndex].Name;

                    ImGui.SetTooltip($"{section.Name}  {section.StartTime:0.00}s -> {section.EndTime:0.00}s  ({section.Duration:0.00}s)\nthen {next}");
                    break;
                }
                break;
            }
            case TimelineRowKind.Header or TimelineRowKind.Slot:
            {
                foreach (var segment in row.Kind == TimelineRowKind.Slot ? row.Segments : animation.Segments)
                {
                    if (!segment.IsActiveAt(time)) continue;

                    var loop = segment.LoopCount > 1 ? $"  {Settings.LoopIcon} {segment.LoopCount}" : string.Empty;
                    ImGui.SetTooltip($"{segment.Sequence.Name}\n{segment.StartPos:0.00}s -> {segment.EndPos:0.00}s{loop}\n{segment.Sequence.FrameCount} frames @ {segment.Sequence.FrameRate:0.#} fps");
                    break;
                }
                break;
            }
            case TimelineRowKind.Player when row.Clock is { } clock:
            {
                ImGui.SetTooltip($"{clock.Time:0.00}s of {animation.Duration:0.00}s at {clock.PlayRate:0.##}x\ndrag to seek");
                break;
            }
        }
    }
}
