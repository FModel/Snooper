using ImGuiNET;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.UI;

namespace Editor.Modals;

/// <summary>
/// List of every material the scene has built. Entries carry the cache key rather than the container,
/// since that is what a section is repointed at.
/// </summary>
public sealed class MaterialPickerModal : AssetPickerModal<MaterialPickerModal.Entry>
{
    public static MaterialPickerModal Instance { get; } = new();

    public sealed record Entry(string Key, MaterialDataContainer Container);

    protected override string Title => "Select Material";
    protected override string ItemNoun => "material";

    protected override IEnumerable<Entry> Enumerate()
    {
        foreach (var (key, container) in MaterialCache.GetLoaded())
        {
            yield return new Entry(key, container);
        }
    }

    protected override string NameOf(Entry item) => item.Container.Name;

    protected override Entry? DrawItems(IReadOnlyList<Entry> items)
    {
        Entry? picked = null;

        for (var i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            var container = entry.Container;

            ImGui.PushID(i);
            if (ImGui.Selectable("##Entry", false, ImGuiSelectableFlags.AllowOverlap)) picked = entry;
            if (ImGui.IsItemHovered()) EditorUI.Tooltip($"{container.Name}\n{entry.Key}");

            // drawn over the selectable so the whole row stays one hit target
            ImGui.SameLine();
            ImGui.TextUnformatted(container.Name);

            var detail = $"{container.LayerCount} layer{(container.LayerCount != 1 ? "s" : "")}, {container.BlendMode.GetDescription()}";
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(detail).X);
            ImGui.TextDisabled(detail);

            ImGui.PopID();
        }

        return picked;
    }
}
