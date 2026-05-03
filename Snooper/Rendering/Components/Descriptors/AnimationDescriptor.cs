using System.Numerics;
using CUE4Parse_Conversion.Animations;
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
    public readonly float Duration;

    public float StartTime;
    public float PlayRate;

    private AnimationDescriptor(AnimationDescriptor other)
    {
        Name = other.Name;
        Path = other.Path;

        Skeleton = other.Skeleton;
        Sequences = other.Sequences;
        Duration = other.Duration;

        StartTime = other.StartTime;
        PlayRate = other.PlayRate;
    }

    public AnimationDescriptor(UAnimationAsset owner, float startTime = 0f, float playRate = 1f)
    {
        Name = owner.Name;
        Path = owner.Owner?.Provider?.FixPath(owner.Owner?.Name ?? owner.GetPathName()) ?? "N/A";

        var animation = owner.ConvertAnims();

        Skeleton = new SkeletonDescriptor(animation.Skeleton.ReferenceSkeleton);
        Skeleton.SetOwner(animation.Skeleton);

        Sequences = new SequenceDescriptor[animation.Sequences.Count];
        for (var i = 0; i < Sequences.Length; i++)
        {
            var sequence = animation.Sequences[i];
            sequence.RetargetTracks(animation.Skeleton);
            Sequences[i] = new SequenceDescriptor(sequence);
        }

        if (Sequences.Length > 0)
            Duration = Sequences[^1].EndTime;

        StartTime = startTime;
        PlayRate = playRate;
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

        var startFrac = Duration > 0f ? StartTime / Duration : 0f;
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

            // colored span
            var sf = Duration > 0f ? seq.StartTime / Duration : 0f;
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
            dl.AddText(new Vector2(markerX + ts, barsTop), markerCol, $"{StartTime:0.00}s");
        }

        ImGui.Spacing();
        var rowH = ImGui.GetTextLineHeightWithSpacing();
        var tblH = Math.Min(Sequences.Length, 8) * rowH + ImGui.GetFrameHeightWithSpacing();
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##SeqTimeline", 7, flags, new Vector2(0, tblH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("Frames", ImGuiTableColumnFlags.WidthFixed, 48f);
            ImGui.TableSetupColumn("FPS", ImGuiTableColumnFlags.WidthFixed, 44f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < Sequences.Length; i++)
            {
                var seq = Sequences[i]; ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{i}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(seq.Name);
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.StartTime:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.EndTime:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.Duration:F2}s");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.FrameCount}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{seq.FrameRate:F1}");
            }
            ImGui.EndTable();
        }
    }

    public object Clone() => new AnimationDescriptor(this);
}
