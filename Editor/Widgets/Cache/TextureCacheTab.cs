using System.Numerics;
using Editor.Managers;
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

    private const float RefreshInterval = 0.25f; // the figures above the list are formatted strings

    private static readonly string _filterHint = $"{Settings.MagnifyingGlassIcon}  Filter by name";

    private static readonly string[] _textureStates = ["Resident", "Evictable", "Loading", "Failed"];
    private static readonly uint[] _textureColors = [ToColor(Settings.GreenColor), ToColor(Settings.YellowColor), ToColor(Settings.OrangeColor), ToColor(Settings.RedColor)];
    private static readonly string[] _containerStates = ["Ready", "Waiting", "Stalled"];
    private static readonly uint[] _containerColors = [ToColor(Settings.GreenColor), ToColor(Settings.OrangeColor), ToColor(Settings.RedColor)];

    private static readonly Comparison<TextureCache.TextureEntry> _byMemory = (a, b) =>
    {
        var order = b.Texture.Allocated.CompareTo(a.Texture.Allocated);
        return order != 0 ? order : string.Compare(a.Texture.Name, b.Texture.Name, StringComparison.OrdinalIgnoreCase);
    };

    private static readonly Comparison<TextureCache.ContainerEntry> _bySections = (a, b) =>
    {
        var order = b.Sections.Count.CompareTo(a.Sections.Count);
        return order != 0 ? order : string.Compare(a.Container.Name, b.Container.Name, StringComparison.OrdinalIgnoreCase);
    };

    private readonly List<TextureCache.TextureEntry> _textures = [];
    private readonly List<TextureCache.ContainerEntry> _containers = [];

    private EView _view;
    private string _search = string.Empty;
    private int _state = -1; // the state the list is narrowed to, none below zero

    private TextureCache.TextureEntry? _selectedTexture;
    private TextureCache.ContainerEntry? _selectedContainer;

    private readonly int[] _textureCounts = new int[4];
    private readonly long[] _textureBytes = new long[4];
    private readonly string[] _textureValues = [string.Empty, string.Empty, string.Empty, string.Empty];
    private readonly int[] _containerCounts = new int[3];
    private readonly string[] _containerValues = [string.Empty, string.Empty, string.Empty];
    private readonly MemoryChart.Segment[] _segments = new MemoryChart.Segment[2];
    private readonly MemoryChart.Tile[] _totals = new MemoryChart.Tile[4];
    private float _sinceRefresh = float.MaxValue;

    public void Draw(EditorManager editor)
    {
        Collect();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });

        var gap = new Vector2(0f, MemoryChart.Gap);

        MemoryChart.Tiles(_totals);
        ImGui.Dummy(gap);
        MemoryChart.Bar(_segments);
        ImGui.Dummy(gap);

        var states = _view == EView.Textures ? _textureStates : _containerStates;
        var colors = _view == EView.Textures ? _textureColors : _containerColors;
        var values = _view == EView.Textures ? _textureValues : _containerValues;
        for (var i = 0; i < states.Length; i++)
        {
            ImGui.PushID(i);
            if (MemoryChart.Row(colors[i], states[i], values[i], selected: _state == i)) _state = _state == i ? -1 : i;
            ImGui.PopID();
        }

        ImGui.Dummy(gap);
        if (MemoryChart.Chip("Textures", _view == EView.Textures)) Show(EView.Textures);
        ImGui.SameLine(0f, 0.2f * MemoryChart.Unit);
        if (MemoryChart.Chip("Containers", _view == EView.Containers)) Show(EView.Containers);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##TextureCacheFilter", _filterHint, ref _search, 128, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.Dummy(gap);
        ImGui.PopStyleVar();

        var hasDetails = _view == EView.Textures ? _selectedTexture is not null : _selectedContainer is not null;
        var detailsHeight = MemoryChart.Unit * 9f;
        var listSize = new Vector2(0f, hasDetails ? MathF.Max(ImGui.GetContentRegionAvail().Y - detailsHeight - ImGui.GetStyle().ItemSpacing.Y, detailsHeight) : 0f);

        if (_view == EView.Textures) DrawTextures(listSize);
        else DrawContainers(listSize);

        if (_view == EView.Textures && _selectedTexture is { } texture) DrawTextureDetails(texture);
        else if (_view == EView.Containers && _selectedContainer is { } container) DrawContainerDetails(container);
    }

    /// <summary>
    /// Rebuilt every frame: the cache moves under us, and a selection that left the cache must not be kept alive here.
    /// </summary>
    private void Collect()
    {
        _sinceRefresh += ImGui.GetIO().DeltaTime;
        var refresh = _sinceRefresh >= RefreshInterval;
        if (refresh)
        {
            _sinceRefresh = 0f;
            Array.Clear(_textureCounts);
            Array.Clear(_textureBytes);
            Array.Clear(_containerCounts);
        }

        var textureAlive = false;
        _textures.Clear();
        foreach (var entry in TextureCache.Textures)
        {
            var state = StateOf(entry);
            if (refresh)
            {
                _textureCounts[state]++;
                if (entry.Bindless is not null) _textureBytes[state] += entry.Texture.Allocated;
            }

            textureAlive |= entry == _selectedTexture;
            if (_view == EView.Textures && Matches(entry.Texture.Name, state)) _textures.Add(entry);
        }

        var containerAlive = false;
        _containers.Clear();
        foreach (var entry in TextureCache.Containers)
        {
            var state = StateOf(entry);
            if (refresh) _containerCounts[state]++;

            containerAlive |= entry == _selectedContainer;
            if (_view == EView.Containers && Matches(entry.Container.Name, state)) _containers.Add(entry);
        }

        if (!textureAlive) _selectedTexture = null;
        if (!containerAlive) _selectedContainer = null;

        _textures.Sort(_byMemory);
        _containers.Sort(_bySections);

        if (!refresh) return;

        for (var i = 0; i < _textureValues.Length; i++)
        {
            _textureValues[i] = _textureBytes[i] > 0 ? $"{_textureCounts[i]:N0}   {_textureBytes[i].GetReadableSize()}" : $"{_textureCounts[i]:N0}";
        }

        for (var i = 0; i < _containerValues.Length; i++)
        {
            _containerValues[i] = $"{_containerCounts[i]:N0}";
        }

        const long budget = TextureCache.TextureBudgetBytes;
        _segments[0] = new MemoryChart.Segment(_textureStates[0], _textureColors[0], (float) _textureBytes[0] / budget);
        _segments[1] = new MemoryChart.Segment(_textureStates[1], _textureColors[1], (float) _textureBytes[1] / budget);

        _totals[0] = new MemoryChart.Tile("In Use", _textureBytes[0].GetReadableSize());
        _totals[1] = new MemoryChart.Tile("Evictable", _textureBytes[1].GetReadableSize());
        _totals[2] = new MemoryChart.Tile("Budget", budget.GetReadableSize());
        _totals[3] = new MemoryChart.Tile("Textures", $"{TextureCache.Textures.Count:N0} in {TextureCache.Containers.Count:N0} containers");
    }

    private bool Matches(string name, int state)
    {
        return (_state < 0 || _state == state) && (_search.Length == 0 || name.Contains(_search, StringComparison.OrdinalIgnoreCase));
    }

    private void Show(EView view)
    {
        if (_view == view) return;

        _view = view;
        _search = string.Empty; // a filter written for one list hides everything in the other
        _state = -1;
    }

    private void DrawTextures(Vector2 size)
    {
        if (ImGui.BeginChild("##CachedTextures", size))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });
            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_textures.Count, MemoryChart.RowHeight);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var entry = _textures[i];
                        var texture = entry.Texture;
                        var value = entry.Bindless is null
                            ? $"{texture.Width}x{texture.Height}   {texture.FormatName}"
                            : $"{texture.Width}x{texture.Height}   {texture.FormatName}   {texture.GetFormattedSpace()}";

                        ImGui.PushID(i);
                        if (MemoryChart.Row(_textureColors[StateOf(entry)], texture.Name, value, selected: entry == _selectedTexture))
                        {
                            _selectedTexture = entry == _selectedTexture ? null : entry;
                        }
                        ImGui.PopID();
                    }
                }
                clipper.End();
                clipper.Destroy();
            }
            ImGui.PopStyleVar();
        }
        ImGui.EndChild();
    }

    private void DrawContainers(Vector2 size)
    {
        if (ImGui.BeginChild("##CachedContainers", size))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });
            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_containers.Count, MemoryChart.RowHeight);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var entry = _containers[i];

                        ImGui.PushID(i);
                        if (MemoryChart.Row(_containerColors[StateOf(entry)], entry.Container.Name, Describe(entry), selected: entry == _selectedContainer))
                        {
                            _selectedContainer = entry == _selectedContainer ? null : entry;
                        }
                        ImGui.PopID();
                    }
                }
                clipper.End();
                clipper.Destroy();
            }
            ImGui.PopStyleVar();
        }
        ImGui.EndChild();
    }

    private void DrawTextureDetails(TextureCache.TextureEntry entry)
    {
        if (ImGui.BeginChild("##CachedTextureDetails", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            if (entry.Texture.GetPointer() != IntPtr.Zero) entry.Texture.DrawControls();
            else ImGui.TextUnformatted(entry.Texture.Name);

            // a container that waits for this texture counts too, it holds the entry even before the handle exists
            ImGui.SeparatorText("Used By");
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });
            var users = 0;
            foreach (var container in TextureCache.Containers)
            {
                if (!container.Textures.Contains(entry)) continue;

                ImGui.PushID(users++);
                if (MemoryChart.Row(_containerColors[StateOf(container)], container.Container.Name, Describe(container)))
                {
                    Show(EView.Containers);
                    _selectedContainer = container;
                }
                ImGui.PopID();
            }
            ImGui.PopStyleVar();

            if (users == 0) ImGui.TextDisabled("No container, it stays resident until the budget needs the room.");
        }
        ImGui.EndChild();
    }

    private void DrawContainerDetails(TextureCache.ContainerEntry entry)
    {
        if (ImGui.BeginChild("##CachedContainerDetails", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            ImGui.TextUnformatted(entry.Container.Name);
            EditorUI.Caption(entry.Key, $"waiting for {entry.Remaining:N0} of {entry.Textures.Count:N0} textures");

            ImGui.SeparatorText("Textures");
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });
            for (var i = 0; i < entry.Textures.Count; i++)
            {
                var texture = entry.Textures[i];

                ImGui.PushID(i);
                if (MemoryChart.Row(_textureColors[StateOf(texture)], texture.Texture.Name, _textureStates[StateOf(texture)]))
                {
                    Show(EView.Textures);
                    _selectedTexture = texture;
                }
                ImGui.PopID();
            }
            ImGui.PopStyleVar();

            if (entry.Textures.Count == 0) ImGui.TextDisabled("This material samples no texture.");
        }
        ImGui.EndChild();
    }

    private static string Describe(TextureCache.ContainerEntry entry)
    {
        return $"{entry.Textures.Count:N0} texture{(entry.Textures.Count != 1 ? "s" : "")}   {entry.Sections.Count:N0} section{(entry.Sections.Count != 1 ? "s" : "")}";
    }

    private static int StateOf(TextureCache.TextureEntry entry)
    {
        if (entry.Failed) return 3;
        if (entry.Bindless is null) return 2;
        return entry.Evictable is not null ? 1 : 0;
    }

    private static int StateOf(TextureCache.ContainerEntry entry)
    {
        if (entry.Completed) return 0;

        foreach (var texture in entry.Textures)
        {
            if (texture.Failed) return 2; // a failed texture never lands, neither does the container
        }

        return 1;
    }

    private static uint ToColor(Vector4 color) => MemoryChart.Color(color.X, color.Y, color.Z);
}
