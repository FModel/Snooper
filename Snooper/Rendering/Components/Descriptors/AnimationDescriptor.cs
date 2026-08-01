using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;
using ImGuiNET;
using Snooper.UI;

namespace Snooper.Rendering.Components.Descriptors;

public sealed class AnimationDescriptor : IControllable, ICloneable
{
    public string Name { get; }
    public string Path { get; }

    public readonly SkeletonDescriptor Skeleton;
    public readonly SequenceDescriptor[] Sequences;
    public readonly AnimationSectionDescriptor[] Sections;
    public readonly NotifyDescriptor[] Notifies;
    public readonly float Duration;

    public float PlayPosition; // where the playhead is put when the animation is set or rewound
    public float PlayRate; // how fast the playhead crosses the timeline, which never changes its Duration

    private AnimationDescriptor(AnimationDescriptor other)
    {
        Name = other.Name;
        Path = other.Path;

        Skeleton = other.Skeleton;
        Sequences = other.Sequences;
        Sections = other.Sections;
        Notifies = other.Notifies;
        Duration = other.Duration;

        PlayPosition = other.PlayPosition;
        PlayRate = other.PlayRate;
    }

    public AnimationDescriptor(UAnimationAsset owner, float playPosition = 0f, float playRate = 1f)
    {
        Name = owner.Name;
        Path = owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName()) ?? "N/A";

        var skeleton = owner.Skeleton.Load<USkeleton>() ?? throw new InvalidOperationException($"Failed to load skeleton for animation asset {owner.Name}");
        Skeleton = new SkeletonDescriptor(skeleton.ReferenceSkeleton);
        Skeleton.SetOwner(skeleton);

        var cache = new Dictionary<UAnimSequence, CAnimSequence>(ReferenceEqualityComparer.Instance);
        var sequences = new List<SequenceDescriptor>();
        var duration = 0f;
        switch (owner)
        {
            case UAnimMontage montage:
            {
                foreach (var slot in montage.SlotAnimTracks)
                {
                    AddTrack(slot.AnimTrack, slot.SlotName.Text);
                }

                var sections = montage.CompositeSections;

                var starts = new float[sections.Length];
                for (var i = 0; i < starts.Length; i++)
                {
                    starts[i] = sections[i].GetTime();
                }

                Sections = new AnimationSectionDescriptor[sections.Length];
                for (var i = 0; i < Sections.Length; i++)
                {
                    var end = duration;
                    foreach (var start in starts)
                    {
                        if (start > starts[i] && start < end) end = start;
                    }

                    var name = sections[i].NextSectionName;
                    var next = name.IsNone ? -1 : Array.FindIndex(sections, section => section.SectionName == name);

                    Sections[i] = new AnimationSectionDescriptor(sections[i].SectionName.Text, starts[i], end, next);
                }

                break;
            }
            case UAnimComposite composite:
            {
                AddTrack(composite.AnimationTrack, null);
                Sections = [];
                break;
            }
            case UAnimSequence sequence:
            {
                AddSequence(sequence);
                Sections = [];
                break;
            }
            default:
            {
                Sections = [];
                break;
            }
        }

        Sequences = sequences.ToArray();
        Duration = duration;

        if (owner is UAnimSequenceBase animSequence)
        {
            Notifies = new NotifyDescriptor[animSequence.Notifies.Length];
            for (var i = 0; i < Notifies.Length; i++)
            {
                Notifies[i] = new NotifyDescriptor(animSequence.Notifies[i]);
            }
        }
        else Notifies = [];

        PlayPosition = playPosition;
        PlayRate = playRate;

        void AddTrack(FAnimTrack track, string? slotName)
        {
            foreach (var segment in track.AnimSegments)
            {
                if (!segment.AnimReference.TryLoad<UAnimSequence>(out var sequence))
                    continue;

                AddSequence(sequence, segment, slotName);
            }
        }

        void AddSequence(UAnimSequence sequence, FAnimSegment? segment = null, string? slotName = null)
        {
            if (!TryConvert(sequence, out var clip)) return;

            var descriptor = new SequenceDescriptor(clip, segment, slotName);
            duration = MathF.Max(duration, descriptor.EndPos);
            sequences.Add(descriptor);
        }

        bool TryConvert(UAnimSequence sequence, [MaybeNullWhen(false)] out CAnimSequence clip)
        {
            if (cache.TryGetValue(sequence, out clip)) return true;

            clip = skeleton.ConvertAnims(sequence).Sequences.FirstOrDefault();
            if (clip is null) return false;

            clip.RetargetTracks(skeleton);
            cache[sequence] = clip;
            return true;
        }
    }

    public float Follow(float from, float to)
    {
        if (to <= from) return to;

        var index = SectionAt(from);
        if (index < 0) return to;

        var section = Sections[index];
        if (to < section.EndTime || section.NextIndex < 0) return to;

        var next = Sections[section.NextIndex];
        var carried = next.StartTime + (to - section.EndTime);
        return carried < next.EndTime ? carried : next.StartTime;

        int SectionAt(float time)
        {
            for (var i = 0; i < Sections.Length; i++)
            {
                if (Sections[i].IsActiveAt(time)) return i;
            }

            return -1;
        }
    }

    public bool TryGetSequence(uint skeletonIndex, float time, [MaybeNullWhen(false)] out SequenceDescriptor sequence)
    {
        sequence = null;
        foreach (var s in Sequences)
        {
            if (s.IsActiveAt(time) && s.IsAnimatingBone(skeletonIndex))
            {
                sequence = s;
                break;
            }
        }

        return sequence != null;
    }

    public void DrawControls()
    {
        DrawHeader();

        ImGui.Spacing();
        ImGui.SeparatorText($"Bones  ({Skeleton.BoneCount})");
        Skeleton.DrawControls();

        ImGui.Spacing();
        ImGui.SeparatorText($"Sequences ({Sequences.Length})");
        DrawSequenceTimeline();

        if (Sections.Length == 0) return;

        ImGui.Spacing();
        ImGui.SeparatorText($"Sections ({Sections.Length})");
        DrawSectionTable();
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted(Name);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted($"  ({Duration:0.00} seconds @ {PlayRate:0.00}x)");

        ImGui.SetWindowFontScale(0.85f);
        ImGui.TextUnformatted($"Animation: {Path}");
        ImGui.TextUnformatted($"Skeleton: {Skeleton.Path}");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();
    }

    private void DrawSequenceTimeline()
    {
        if (Sequences.Length == 0)
        {
            ImGui.TextDisabled("No sequences.");
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

        var startFrac = Duration > 0f ? PlayPosition / Duration : 0f;
        var markerX   = 0f;
        var barsTop   = 0f;
        var barsBot   = 0f;

        for (var i = 0; i < Sequences.Length; i++)
        {
            var seq = Sequences[i];
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
            var sf = Duration > 0f ? seq.StartPos / Duration : 0f;
            var wf = Duration > 0f ? seq.Duration / Duration : 1f;
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
            dl.AddText(new Vector2(markerX + ts, barsTop), markerCol, $"{PlayPosition:0.00}s");
        }

        ImGui.Spacing();
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = Math.Min(Sequences.Length, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##SeqTimeline", 9, flags, new Vector2(0, tblH)))
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

            for (var i = 0; i < Sequences.Length; i++)
            {
                var seq = Sequences[i]; ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(seq.Name);
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.StartPos:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.EndPos:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.Duration:F2}s");

                ImGui.TableNextColumn();
                var source = $"{seq.SourceStart:F2}-{seq.SourceEnd:F2} of {seq.SourceLength:F2}s";
                if (seq.IsClipped) ImGui.TextColored(Settings.OrangeColor, source);
                else ImGui.TextUnformatted(source);

                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.FrameCount}");

                ImGui.TableNextColumn();
                if (seq.KeyCount != seq.FrameCount) ImGui.TextColored(Settings.OrangeColor, $"{seq.KeyCount}");
                else ImGui.TextUnformatted($"{seq.KeyCount}");

                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.FrameRate:F1}");
            }
            ImGui.EndTable();
        }
    }

    private void DrawSectionTable()
    {
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = Math.Min(Sections.Length, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##SecTimeline", 6, flags, new Vector2(0, tblH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("Next", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (var i = 0; i < Sections.Length; i++)
            {
                var section = Sections[i]; ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(section.Name);
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{section.StartTime:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{section.EndTime:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{section.Duration:F2}s");

                ImGui.TableNextColumn();
                if (section.NextIndex < 0) ImGui.TextDisabled("End");
                else if (section.NextIndex == i) ImGui.TextUnformatted($"{Settings.LoopIcon} {section.Name}");
                else ImGui.TextUnformatted(Sections[section.NextIndex].Name);
            }
            ImGui.EndTable();
        }
    }

    public object Clone() => new AnimationDescriptor(this);
}
