using System.Globalization;
using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper.UI;

namespace Editor.Widgets.Custom;

internal sealed class DeadByDaylightMapCard : IViewportCard
{
    public string Title => "Dead by Daylight Map";
    public bool IsOpen { get; set; } = true;

    private int _mapId;
    private string _seedInput = "1";
    private int _appliedSeed;

    public unsafe void Draw(EditorManager editor)
    {
        if (editor.DeadByDaylightMap is not { } map)
            return;

        ImGui.PushID(map.Id);
        ImGui.TextDisabled(map.Name);

        if (_mapId != map.Id || _appliedSeed != map.Seed)
        {
            _mapId = map.Id;
            _appliedSeed = map.Seed;
            _seedInput = map.Seed.ToString(CultureInfo.InvariantCulture);
        }

        ImGui.BeginDisabled(map.IsRegenerating);
        var apply = false;
        EditorUI.PropertyValueTable("Map", () =>
        {
            EditorUI.Property("Seed");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##Seed", ref _seedInput, 11, ImGuiInputTextFlags.CallbackCharFilter, FilterSeedCharacter);
            apply = (ImGui.IsItemActive() || ImGui.IsItemDeactivated()) && ImGui.IsKeyPressed(ImGuiKey.Enter);
        }, indent: false);

        var validSeed = int.TryParse(_seedInput, NumberStyles.None, CultureInfo.InvariantCulture, out var seed);
        if (!validSeed && ImGui.IsItemHovered())
            EditorUI.Tooltip($"Enter a seed between 0 and {int.MaxValue}.");

        var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, ImGui.GetFrameHeight());
        ImGui.BeginDisabled(!validSeed || seed == map.Seed);
        apply |= ImGui.Button(map.IsRegenerating ? "Regenerating.." : "Regenerate", buttonSize);
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Randomize", buttonSize))
        {
            seed = Random.Shared.Next();
            _seedInput = seed.ToString(CultureInfo.InvariantCulture);
            validSeed = true;
            apply = true;
        }
        ImGui.EndDisabled();

        if (apply && validSeed)
            map.Regenerate(seed);

        if (map.GenerationError != null)
            ImGui.TextWrapped(map.GenerationError);

        ImGui.PopID();
    }

    private static unsafe int FilterSeedCharacter(ImGuiInputTextCallbackData* data) => char.IsAsciiDigit((char) data->EventChar) ? 0 : 1;
}
