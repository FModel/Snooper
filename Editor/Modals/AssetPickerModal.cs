using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.UI;

namespace Editor.Modals;

/// <summary>
/// Modal shell for picking one asset out of what the caches have already decoded: title bar, filter,
/// scrolling body and footer. Subclasses supply the source collection and how an entry is drawn.
/// Like every modal it is a singleton drawn once per frame from the manager, and hands the choice back
/// through the callback given to <see cref="Open"/>.
/// </summary>
public abstract class AssetPickerModal<T> where T : class
{
    protected abstract string Title { get; }

    /// <summary>Noun for the empty and count lines, e.g. "texture".</summary>
    protected abstract string ItemNoun { get; }

    protected abstract IEnumerable<T> Enumerate();
    protected abstract string NameOf(T item);

    /// <summary>Draws the filtered entries. Returns the picked one, or null.</summary>
    protected abstract T? DrawItems(IReadOnlyList<T> items);

    protected readonly List<T> Items = [];

    private Action<T>? _onPicked;
    private string _search = "";
    private bool _dirty = true;
    private bool _openPopup;
    private bool _modalOpen;

    /// <summary>Opens the picker. The callback fires on the frame a choice is made.</summary>
    public void Open(Action<T> onPicked)
    {
        _onPicked = onPicked;
        _search = "";
        _dirty = true;
        _openPopup = true;
    }

    public void Draw()
    {
        // ### gives the modal a real title bar while keeping its identity stable. Both calls must be
        // given the exact same string: ImGui restarts the hash *at* the ###, so "Title###id" and "id"
        // are different ids and the popup would never open.
        var label = $"{Title}###{GetType().Name}";

        if (_openPopup)
        {
            ImGui.OpenPopup(label);
            _modalOpen = true;
            _openPopup = false;
        }

        if (!_modalOpen) return;

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(viewport.WorkSize * 0.5f, ImGuiCond.Appearing);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));

        var open = true;
        if (!ImGui.BeginPopupModal(label, ref open, ImGuiWindowFlags.NoSavedSettings))
        {
            if (!open) Close();
            return;
        }

        if (_dirty) Refresh();
        DrawFilterBar();

        T? picked = null;
        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        if (ImGui.BeginChild("##PickerBody", new Vector2(0, -footer), ImGuiChildFlags.FrameStyle))
        {
            if (Items.Count == 0) EditorUI.CenteredText($"No {ItemNoun} matches.", ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
            else picked = DrawItems(Items);
        }
        ImGui.EndChild();

        DrawFooter();

        if (picked is not null)
        {
            // take the callback before invoking it, so a handler that reopens the picker is not cleared
            var callback = _onPicked;
            Close();
            ImGui.CloseCurrentPopup();
            callback?.Invoke(picked);
        }
        else if (!open)
        {
            Close();
        }

        ImGui.EndPopup();
    }

    private void Close()
    {
        _modalOpen = false;
        _onPicked = null;
        Items.Clear();
    }

    private void Refresh()
    {
        Items.Clear();

        var isSearching = !string.IsNullOrWhiteSpace(_search);
        foreach (var item in Enumerate())
        {
            if (isSearching && !NameOf(item).Contains(_search, StringComparison.OrdinalIgnoreCase)) continue;
            Items.Add(item);
        }

        Items.Sort((a, b) => string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase));
        _dirty = false;
    }

    private void DrawFilterBar()
    {
        var count = $"{Items.Count} {ItemNoun}{(Items.Count != 1 ? "s" : "")}";
        var countWidth = ImGui.CalcTextSize(count).X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();

        ImGui.SetNextItemWidth(MathF.Max(ImGui.GetContentRegionAvail().X - countWidth - spacing * 2, ImGui.GetFrameHeight() * 4));
        if (ImGui.InputTextWithHint("##PickerFilter", $"{Settings.MagnifyingGlassIcon}  Filter", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll))
        {
            _dirty = true;
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - countWidth + spacing);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(count);
    }

    private static void DrawFooter()
    {
        // TODO: browse the provider's files instead of the caches, for assets the scene never loaded
        ImGui.BeginDisabled();
        ImGui.Button($"{Settings.FolderOpenIcon}  Load from Files");
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            EditorUI.Tooltip("Load from Files\nPick an asset the scene has not loaded yet - not implemented");
        }

        var cancel = ImGui.CalcTextSize("Cancel").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - cancel + ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
    }
}
