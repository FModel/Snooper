using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using ImGuiNET;

namespace Snooper.Rendering.Components.Descriptors.Animations;

public sealed class MontageDescriptor : CompositeBaseDescriptor
{
    public readonly SectionDescriptor[] Sections;

    public MontageDescriptor(UAnimMontage owner) : base(owner)
    {
        foreach (var slot in owner.SlotAnimTracks)
        {
            AddTrack(slot.AnimTrack, slot.SlotName.Text);
        }

        var sections = owner.CompositeSections;

        var starts = new float[sections.Length];
        for (var i = 0; i < starts.Length; i++)
        {
            starts[i] = sections[i].GetTime();
        }

        Sections = new SectionDescriptor[sections.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            // a section runs until the next one starts, or until the montage does
            var end = Duration;
            foreach (var start in starts)
            {
                if (start > starts[i] && start < end) end = start;
            }

            var name = sections[i].NextSectionName;
            var next = name.IsNone ? -1 : Array.FindIndex(sections, section => section.SectionName == name);

            Sections[i] = new SectionDescriptor(sections[i].SectionName.Text, starts[i], end, next);
        }
    }

    public override float Follow(float from, float to)
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
            var found = -1;
            for (var i = 0; i < Sections.Length; i++)
            {
                if (Sections[i].StartTime > time) continue;
                if (found < 0 || Sections[i].StartTime > Sections[found].StartTime) found = i;
            }

            return found;
        }
    }

    public override void DrawControls(float time)
    {
        base.DrawControls(time);

        if (Sections.Length == 0) return;

        ImGui.Spacing();
        ImGui.SeparatorText($"Sections ({Sections.Length})");
        DrawSectionTable();
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
}
