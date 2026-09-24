using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.Extensions;
using Snooper.Rendering.Cache;
using Snooper.UI;

namespace Editor.Widgets.Cache;

public class TextureCacheTab : ICacheTab
{
    public string Title => "Textures";

    private enum EView
    {
        Textures,
        Containers,
    }

    private const float DetailsHeight = 170f;

    private readonly List<TextureCache.TextureEntry> _textures = [];
    private readonly List<TextureCache.ContainerEntry> _containers = [];
    private readonly Comparison<TextureCache.TextureEntry> _compareTextures;
    private readonly Comparison<TextureCache.ContainerEntry> _compareContainers;

    private EView _view;
    private string _search = string.Empty;
    private int _sortColumn;
    private bool _sortDescending;

    private TextureCache.TextureEntry? _selectedTexture;
    private TextureCache.ContainerEntry? _selectedContainer;

    public TextureCacheTab()
    {
        _compareTextures = CompareTextures;
        _compareContainers = CompareContainers;
    }

    public void Draw()
    {
        Collect();
        DrawSummary();
        DrawToolbar();

        var hasDetails = _view == EView.Textures ? _selectedTexture is not null : _selectedContainer is not null;
        var listSize = new Vector2(0f, hasDetails ? MathF.Max(ImGui.GetContentRegionAvail().Y - DetailsHeight - ImGui.GetStyle().ItemSpacing.Y, DetailsHeight) : 0f);

        if (_view == EView.Textures)
        {
            DrawTextures(listSize);
            if (_selectedTexture is { } texture) DrawTextureDetails(texture);
        }
        else
        {
            DrawContainers(listSize);
            if (_selectedContainer is { } container) DrawContainerDetails(container);
        }
    }

    /// <summary>
    /// Rebuilt every frame: the cache moves under us, and a selection that left the cache must not be kept alive here.
    /// </summary>
    private void Collect()
    {
        var textureAlive = false;
        _textures.Clear();
        foreach (var entry in TextureCache.Textures)
        {
            textureAlive |= entry == _selectedTexture;
            if (Matches(entry.Texture.Name, StateOf(entry).Label)) _textures.Add(entry);
        }

        var containerAlive = false;
        _containers.Clear();
        foreach (var entry in TextureCache.Containers)
        {
            containerAlive |= entry == _selectedContainer;
            if (Matches(entry.Container.Name, StateOf(entry).Label)) _containers.Add(entry);
        }

        if (!textureAlive) _selectedTexture = null;
        if (!containerAlive) _selectedContainer = null;
    }

    private bool Matches(string name, string state)
    {
        return _search.Length == 0 || name.Contains(_search, StringComparison.OrdinalIgnoreCase) || state.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawSummary()
    {
        var resident = TextureCache.ResidentBytes;
        const long budget = TextureCache.TextureBudgetBytes;
        ImGui.ProgressBar(budget > 0 ? MathF.Min(1f, (float) resident / budget) : 0f, new Vector2(-1f, 0f), $"{resident.GetReadableSize()} / {budget.GetReadableSize()}");

        var sections = 0;
        var waiting = 0;
        foreach (var container in TextureCache.Containers)
        {
            sections += container.Sections.Count;
            if (!container.Completed) waiting++;
        }

        EditorUI.Caption(
            $"{TextureCache.LoadedTextureCount:N0} resident, {TextureCache.EvictableTextureCount:N0} of them evictable, {TextureCache.PendingTextureCount:N0} loading",
            $"{TextureCache.Containers.Count:N0} containers, {waiting:N0} waiting, used by {sections:N0} sections, {TextureCache.NotifyQueueCount:N0} to notify");
    }

    private void DrawToolbar()
    {
        if (ImGui.RadioButton($"Textures ({TextureCache.Textures.Count:N0})", _view == EView.Textures)) Show(EView.Textures);
        ImGui.SameLine();
        if (ImGui.RadioButton($"Containers ({TextureCache.Containers.Count:N0})", _view == EView.Containers)) Show(EView.Containers);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##TextureCacheFilter", $"{Settings.MagnifyingGlassIcon}  Filter by name or state", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll);
    }

    private void Show(EView view)
    {
        if (_view == view) return;

        _view = view;
        _search = string.Empty; // a filter written for one list hides everything in the other
    }

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit;

    private void DrawTextures(Vector2 size)
    {
        if (!ImGui.BeginTable("##CachedTextures", 7, TableFlags, size)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Size");
        ImGui.TableSetupColumn("Format");
        ImGui.TableSetupColumn("Mips");
        ImGui.TableSetupColumn("Memory", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn("Refs");
        ImGui.TableSetupColumn("State");
        ImGui.TableHeadersRow();

        ReadSort();
        _textures.Sort(_compareTextures);

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_textures.Count);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var entry = _textures[i];
                    var texture = entry.Texture;
                    var (label, color) = StateOf(entry);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (ImGui.Selectable($"{texture.Name}##{i}", entry == _selectedTexture, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selectedTexture = entry == _selectedTexture ? null : entry;
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{texture.Width}x{texture.Height}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(texture.FormatName);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{texture.MipCount}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(entry.Bindless is null ? "-" : texture.GetFormattedSpace());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{entry.RefCount:N0}");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, label);
                }
            }
            clipper.End();
        }

        ImGui.EndTable();
    }

    private void DrawContainers(Vector2 size)
    {
        if (!ImGui.BeginTable("##CachedContainers", 5, TableFlags, size)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Textures");
        ImGui.TableSetupColumn("Waiting For");
        ImGui.TableSetupColumn("Sections", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.PreferSortDescending);
        ImGui.TableSetupColumn("State");
        ImGui.TableHeadersRow();

        ReadSort();
        _containers.Sort(_compareContainers);

        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(_containers.Count);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var entry = _containers[i];
                    var (label, color) = StateOf(entry);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (ImGui.Selectable($"{entry.Container.Name}##{i}", entry == _selectedContainer, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selectedContainer = entry == _selectedContainer ? null : entry;
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{entry.Textures.Count:N0}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{entry.Remaining:N0}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{entry.Sections.Count:N0}");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, label);
                }
            }
            clipper.End();
        }

        ImGui.EndTable();
    }

    private void ReadSort()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.SpecsCount == 0) return;

        _sortColumn = specs.Specs.ColumnIndex;
        _sortDescending = specs.Specs.SortDirection == ImGuiSortDirection.Descending;
    }

    private void DrawTextureDetails(TextureCache.TextureEntry entry)
    {
        if (ImGui.BeginChild("##CachedTextureDetails", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            if (entry.Texture.GetPointer() != IntPtr.Zero) entry.Texture.DrawControls();
            else ImGui.TextUnformatted(entry.Texture.Name);

            // a container that waits for this texture counts too, it holds the entry even before the handle exists
            EditorUI.ListHeader("Used By");
            var users = 0;
            foreach (var container in TextureCache.Containers)
            {
                if (!container.Textures.Contains(entry)) continue;

                users++;
                if (ImGui.Selectable($"{container.Container.Name}##{container.Key}"))
                {
                    Show(EView.Containers);
                    _selectedContainer = container;
                }

                ImGui.SameLine();
                ImGui.TextDisabled($"{container.Sections.Count:N0} section{(container.Sections.Count != 1 ? "s" : "")}");
            }

            if (users == 0) ImGui.TextDisabled("No container, it stays resident until the budget needs the room.");
        }
        ImGui.EndChild();
    }

    private void DrawContainerDetails(TextureCache.ContainerEntry entry)
    {
        if (ImGui.BeginChild("##CachedContainerDetails", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            ImGui.TextUnformatted(entry.Container.Name);
            EditorUI.Caption(entry.Key, $"{entry.Sections.Count:N0} section{(entry.Sections.Count != 1 ? "s" : "")}, waiting for {entry.Remaining:N0} of {entry.Textures.Count:N0} textures");

            EditorUI.ListHeader("Textures");
            foreach (var texture in entry.Textures)
            {
                var (label, color) = StateOf(texture);
                if (ImGui.Selectable($"{texture.Texture.Name}##{texture.Texture.Guid}"))
                {
                    Show(EView.Textures);
                    _selectedTexture = texture;
                }

                ImGui.SameLine();
                ImGui.TextColored(color, label);
            }

            if (entry.Textures.Count == 0) ImGui.TextDisabled("This material samples no texture.");
        }
        ImGui.EndChild();
    }

    private static (string Label, Vector4 Color) StateOf(TextureCache.TextureEntry entry)
    {
        if (entry.Failed) return ("Failed", Settings.RedColor);
        if (entry.Bindless is null) return ("Loading", Settings.OrangeColor);
        if (entry.Evictable is not null) return ("Evictable", Settings.YellowColor);
        return ("Resident", Settings.GreenColor);
    }

    private static (string Label, Vector4 Color) StateOf(TextureCache.ContainerEntry entry)
    {
        if (entry.Completed) return ("Ready", Settings.GreenColor);

        foreach (var texture in entry.Textures)
        {
            if (texture.Failed) return ("Stalled", Settings.RedColor); // a failed texture never lands, neither does the container
        }

        return ("Waiting", Settings.OrangeColor);
    }

    private int CompareTextures(TextureCache.TextureEntry a, TextureCache.TextureEntry b)
    {
        var order = _sortColumn switch
        {
            1 => ((long) a.Texture.Width * a.Texture.Height).CompareTo((long) b.Texture.Width * b.Texture.Height),
            2 => string.CompareOrdinal(a.Texture.FormatName, b.Texture.FormatName),
            3 => a.Texture.MipCount.CompareTo(b.Texture.MipCount),
            4 => a.Texture.Allocated.CompareTo(b.Texture.Allocated),
            5 => a.RefCount.CompareTo(b.RefCount),
            6 => string.CompareOrdinal(StateOf(a).Label, StateOf(b).Label),
            _ => string.Compare(a.Texture.Name, b.Texture.Name, StringComparison.OrdinalIgnoreCase),
        };

        if (order == 0) return string.Compare(a.Texture.Name, b.Texture.Name, StringComparison.OrdinalIgnoreCase);
        return _sortDescending ? -order : order;
    }

    private int CompareContainers(TextureCache.ContainerEntry a, TextureCache.ContainerEntry b)
    {
        var order = _sortColumn switch
        {
            1 => a.Textures.Count.CompareTo(b.Textures.Count),
            2 => a.Remaining.CompareTo(b.Remaining),
            3 => a.Sections.Count.CompareTo(b.Sections.Count),
            4 => string.CompareOrdinal(StateOf(a).Label, StateOf(b).Label),
            _ => string.Compare(a.Container.Name, b.Container.Name, StringComparison.OrdinalIgnoreCase),
        };

        if (order == 0) return string.Compare(a.Container.Name, b.Container.Name, StringComparison.OrdinalIgnoreCase);
        return _sortDescending ? -order : order;
    }
}
