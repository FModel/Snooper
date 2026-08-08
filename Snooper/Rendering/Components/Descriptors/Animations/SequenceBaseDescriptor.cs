using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using ImGuiNET;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public abstract class SequenceBaseDescriptor : AnimationDescriptor
{
    public readonly NotifyDescriptor[] Notifies;
    public float Duration { get; protected set; }

    public abstract IReadOnlyList<SegmentDescriptor> Segments { get; }

    protected SequenceBaseDescriptor(UAnimSequenceBase owner, AnimationDescriptor? outer = null) : base(owner, outer)
    {
        if (owner.Notifies is not { Length: > 0 } notifies)
        {
            Notifies = [];
            return;
        }

        Notifies = new NotifyDescriptor[notifies.Length];
        for (var i = 0; i < Notifies.Length; i++)
        {
            Notifies[i] = new NotifyDescriptor(notifies[i], outer is null);
        }
    }

    public virtual float Follow(float from, float to) => to;

    public bool TryGetSegment(uint skeletonIndex, float time, [MaybeNullWhen(false)] out SegmentDescriptor segment)
    {
        foreach (var s in Segments)
        {
            if (!s.IsActiveAt(time) || !s.IsAnimatingBone(skeletonIndex)) continue;

            segment = s;
            return true;
        }

        segment = null;
        return false;
    }

    protected override string Subtitle => $"  ({Duration:0.00} seconds)";

    public override void DrawControls() => DrawControls(0f);
    public virtual void DrawControls(float time)
    {
        base.DrawControls();

        ImGui.Spacing();
        ImGui.SeparatorText($"Segments ({Segments.Count})");
        DrawSegmentTimeline(time);
    }

    private void DrawSegmentTimeline(float time)
    {
        var segments = Segments;
        if (segments.Count == 0)
        {
            ImGui.TextDisabled("No segments.");
            return;
        }

        uint[] palette = [0xAA_3D_7E_B5, 0xAA_4A_9E_56, 0xAA_C0_70_35, 0xAA_8A_4A_A3, 0xAA_36_99_9E];

        var labelW = ImGui.GetFrameHeight();
        var gapX = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var barW = avail - labelW - gapX;
        var lh = ImGui.GetTextLineHeight();
        var dl = ImGui.GetWindowDrawList();
        var dimCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);

        var startFrac = Duration > 0f ? time / Duration : 0f;
        var markerX   = 0f;
        var barsTop   = 0f;
        var barsBot   = 0f;

        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var p = ImGui.GetCursorScreenPos();
            var bx = p.X + labelW + gapX;

            if (i == 0)
            {
                barsTop = p.Y;
                markerX = bx + startFrac * barW;
            }
            barsBot = p.Y + lh;

            // right-aligned index label
            var idx = $"{i}";
            var idxSz = ImGui.CalcTextSize(idx);
            dl.AddText(p with { X = p.X + labelW - idxSz.X }, dimCol, idx);

            // colored bar
            var sf = Duration > 0f ? seg.StartPos / Duration : 0f;
            var wf = Duration > 0f ? seg.Duration / Duration : 1f;
            dl.AddRectFilled(
                new Vector2(bx + sf * barW, p.Y + 1f),
                new Vector2(bx + (sf + wf) * barW, p.Y + lh - 1f),
                palette[i % palette.Length], 2f);

            ImGui.Dummy(new Vector2(avail, lh));
        }

        if (startFrac > 0f)
        {
            const uint markerCol = 0xFF_40_C8_FF;
            var ts = lh * 0.45f;
            dl.AddLine(new Vector2(markerX - 0.5f, barsTop), new Vector2(markerX - 0.5f, barsBot), markerCol, 1f);
            dl.AddTriangleFilled(
                new Vector2(markerX - ts * 0.5f, barsTop),
                new Vector2(markerX + ts * 0.5f, barsTop),
                new Vector2(markerX, barsTop + ts),
                markerCol);
            dl.AddText(new Vector2(markerX + ts, barsTop), markerCol, $"{time:0.00}s");
        }

        ImGui.Spacing();
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = Math.Min(segments.Count, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##SegTimeline", 9, flags, new Vector2(0, tblH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 118f);
            ImGui.TableSetupColumn("Frames", ImGuiTableColumnFlags.WidthFixed, 48f);
            ImGui.TableSetupColumn("Keys", ImGuiTableColumnFlags.WidthFixed, 40f);
            ImGui.TableSetupColumn("FPS", ImGuiTableColumnFlags.WidthFixed, 44f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < segments.Count; i++)
            {
                var seg = segments[i]; ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(seg.Sequence.Name);
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seg.StartPos:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seg.EndPos:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seg.Duration:F2}s");

                ImGui.TableNextColumn();
                var source = $"{seg.SourceStart:F2}-{seg.SourceEnd:F2} of {seg.Sequence.SourceLength:F2}s";
                if (seg.IsClipped) ImGui.TextColored(Settings.OrangeColor, source);
                else ImGui.TextUnformatted(source);

                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seg.Sequence.FrameCount}");

                ImGui.TableNextColumn();
                if (seg.Sequence.KeyCount != seg.Sequence.FrameCount) ImGui.TextColored(Settings.OrangeColor, $"{seg.Sequence.KeyCount}");
                else ImGui.TextUnformatted($"{seg.Sequence.KeyCount}");

                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seg.Sequence.FrameRate:F1}");
            }
            ImGui.EndTable();
        }
    }
}
