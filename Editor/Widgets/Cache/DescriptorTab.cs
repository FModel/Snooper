using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.UI;

namespace Editor.Widgets.Cache;

internal abstract class DescriptorTab<T> : ICacheTab where T : class, IControllable
{
    public abstract string Title { get; }

    protected abstract string[] Kinds { get; }
    protected abstract uint[] Colors { get; }
    protected abstract IEnumerable<T> Entries { get; }
    protected abstract int KindOf(T entry);
    protected abstract string NameOf(T entry);
    protected abstract string ValueOf(T entry);
    protected abstract void Summarize(List<T> entries, MemoryChart.Tile[] tiles);

    private const float RefreshInterval = 0.25f;

    private const string FilterHint = $"{Settings.MagnifyingGlassIcon}  Filter by name";

    private readonly List<T> _entries = [];
    private readonly List<T> _rows = [];
    private int[] _counts = [];
    private string[] _values = [];
    private MemoryChart.Segment[] _segments = [];
    private readonly MemoryChart.Tile[] _tiles = new MemoryChart.Tile[4];

    private string _search = string.Empty;
    private int _kind = -1;
    private T? _selected;
    private float _sinceRefresh = float.MaxValue;

    public void Draw(EditorManager editor)
    {
        _sinceRefresh += ImGui.GetIO().DeltaTime;
        if (_sinceRefresh >= RefreshInterval)
        {
            _sinceRefresh = 0f;
            Refresh();
        }

        _rows.Clear();
        foreach (var entry in _entries)
        {
            if ((_kind < 0 || KindOf(entry) == _kind) && (_search.Length == 0 || NameOf(entry).Contains(_search, StringComparison.OrdinalIgnoreCase))) _rows.Add(entry);
        }

        var gap = new Vector2(0f, MemoryChart.Gap);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });

        MemoryChart.Tiles(_tiles);
        ImGui.Dummy(gap);
        MemoryChart.Bar(_segments);
        ImGui.Dummy(gap);

        for (var i = 0; i < Kinds.Length; i++)
        {
            ImGui.PushID(i);
            if (MemoryChart.Row(Colors[i], Kinds[i], _values[i], selected: _kind == i)) _kind = _kind == i ? -1 : i;
            ImGui.PopID();
        }

        ImGui.Dummy(gap);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##DescriptorFilter", FilterHint, ref _search, 128, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EscapeClearsAll);
        ImGui.Dummy(gap);
        ImGui.PopStyleVar();

        var detailsHeight = MemoryChart.Unit * 12f;
        var listSize = new Vector2(0f, _selected is not null ? MathF.Max(ImGui.GetContentRegionAvail().Y - detailsHeight - ImGui.GetStyle().ItemSpacing.Y, detailsHeight) : 0f);

        if (ImGui.BeginChild("##Descriptors", listSize))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { Y = 0f });
            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_rows.Count, MemoryChart.RowHeight);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var entry = _rows[i];

                        ImGui.PushID(i);
                        if (MemoryChart.Row(Colors[KindOf(entry)], NameOf(entry), ValueOf(entry), selected: entry == _selected))
                        {
                            _selected = entry == _selected ? null : entry;
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

        if (_selected is not { } selected) return;

        if (ImGui.BeginChild("##DescriptorDetails", Vector2.Zero, ImGuiChildFlags.FrameStyle))
        {
            selected.DrawControls();
        }
        ImGui.EndChild();
    }

    private void Refresh()
    {
        _entries.Clear();
        _entries.AddRange(Entries);
        _entries.Sort((a, b) => string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase));
        if (_selected is not null && !_entries.Contains(_selected)) _selected = null;

        if (_counts.Length != Kinds.Length)
        {
            _counts = new int[Kinds.Length];
            _values = new string[Kinds.Length];
            _segments = new MemoryChart.Segment[Kinds.Length];
        }

        Array.Clear(_counts);
        foreach (var entry in _entries)
        {
            _counts[KindOf(entry)]++;
        }

        for (var i = 0; i < Kinds.Length; i++)
        {
            _values[i] = $"{_counts[i]:N0}";
            _segments[i] = new MemoryChart.Segment(Kinds[i], Colors[i], _entries.Count > 0 ? (float) _counts[i] / _entries.Count : 0f);
        }

        Summarize(_entries, _tiles);
    }
}
