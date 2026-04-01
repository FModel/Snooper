using Editor.Managers;
using ImGuiNET;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;
using Snooper.Rendering.Actors;
using Serilog;
using Snooper;

namespace Editor.Widgets;

public static class InspectorWidget
{
    private const string Title      = "Inspector";
    private const string WarnIcon   = "\uf071";
    private const string FileIcon   = "\uf1c9";
    private const string SearchIcon = "\uf002";

    private readonly record struct FlatEntry(ActorComponent Component, bool Warn);

    private static int    _lastActorId        = -1;
    private static int    _lastComponentCount = -1;
    private static string _search             = "";
    private static bool   _dirty              = true;

    private static readonly List<FlatEntry>  _flatNodes = [];
    private static readonly HashSet<int>     _reachable = [];

    public static void Draw(Actor? selectedActor, ActorComponent? selectedComponent)
    {
        if (ImGui.Begin(Title))
        {
            var actor = selectedActor ?? selectedComponent?.Actor;
            if (actor == null)
            {
                ImGui.TextUnformatted("No actor selected.");
                ImGui.End();
                return;
            }

            var actorId = actor.Id;
            var componentCount = actor.Components.Count;
            if (actorId != _lastActorId || componentCount != _lastComponentCount)
            {
                _lastActorId = actorId;
                _lastComponentCount = componentCount;
                _dirty = true;
            }

            DrawSearchBar();

            ImGui.SeparatorText($"{actor.Name} ({actor.ExportType ?? "N/A"} - {componentCount} Component{(componentCount != 1 ? "s" : "")})");
            DrawClippedTree(actor);

            (selectedComponent ?? actor.RootComponent)?.DrawControls();
        }
        ImGui.End();
    }

    private static void DrawSearchBar()
    {
        var style = ImGui.GetStyle();
        var iconWidth = ImGui.CalcTextSize(SearchIcon).X;
        var inputW = ImGui.GetContentRegionAvail().X - iconWidth - style.ItemSpacing.X;

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(SearchIcon);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputW);
        if (ImGui.InputTextWithHint("##ComponentSearch", "Search...", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll))
        {
            _dirty = true;
        }
    }

    private static void DrawClippedTree(Actor actor)
    {
        if (actor.Components.Count == 0)
        {
            ImGui.TextUnformatted("No components.");
            return;
        }

        ActorComponent? scrollTarget = null;
        if (actor.Components.Any(c => c.ScrollToMe))
        {
            scrollTarget = FindScrollTarget(actor);
            if (scrollTarget != null)
            {
                _dirty = true;
                scrollTarget.ScrollToMe = false;
                Log.Verbose("Found component scroll target: {Name}", scrollTarget.Name);
            }
        }

        var isSearching = !string.IsNullOrWhiteSpace(_search);
        if (_dirty)
        {
            BuildFlatList(actor, isSearching, _search);
            Log.Verbose("Rebuilt component flat list with {Count} entries", _flatNodes.Count);
            _dirty = false;
        }

        var frameH = ImGui.GetFrameHeightWithSpacing();
        var avail  = ImGui.GetContentRegionAvail().Y;
        var minH   = frameH * 3f;
        var maxH   = MathF.Max(minH, avail - frameH * 6f);
        var treeH  = Math.Clamp(frameH * _flatNodes.Count, minH, maxH);

        if (ImGui.BeginChild("##ComponentTreeScroll", new Vector2(-1, treeH), ImGuiChildFlags.FrameStyle))
        {
            if (scrollTarget is { Index: >= 0 })
            {
                var itemY = scrollTarget.Index * frameH;
                var centered = itemY - ImGui.GetWindowHeight() * 0.5f + frameH * 0.5f;
                ImGui.SetScrollY(MathF.Max(0f, centered));
            }

            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_flatNodes.Count, frameH);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var e = _flatNodes[i];
                        DrawFlatNode(e.Component, e.Warn, isSearching);
                    }
                }
                clipper.End();
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private static void DrawFlatNode(ActorComponent component, bool warn, bool isSearching)
    {
        ImGui.PushID(component.Id);

        var style = ImGui.GetStyle();
        var indent = isSearching ? 0f : component.Depth * style.IndentSpacing * 0.5f;
        ImGui.SetCursorPosX(style.WindowPadding.X + indent);
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;

        var hasChildren = !isSearching
                          && component is SpatialComponent sp
                          && sp.Children.Any(c => c.Actor?.Id == component.Actor?.Id);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                    ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding;
        if (component.Selected) flags |= ImGuiTreeNodeFlags.Selected;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        else ImGui.SetNextItemOpen(component.Open, ImGuiCond.Always);

        if (warn) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0f, 1f));
        var nodeOpen = ImGui.TreeNodeEx("##Component", flags, $"{(warn ? $"{WarnIcon}  " : "")}{component.Icon}  {component.Name}");
        component.Open = nodeOpen;
        if (warn) ImGui.PopStyleColor();

        var toggledOpen = ImGui.IsItemToggledOpen();

        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        if (ImGui.BeginPopupContextItem("##ComponentContext"))
        {
            ImGui.TextDisabled(component.Name);
            ImGui.Separator();

            if (ImGui.MenuItem("\uf1c9  Open JSON")) { }
            if (ImGui.MenuItem("\uf24d  Clone")) { }
            if (ImGui.BeginMenu("\uf0c5  Copy"))
            {
                if (ImGui.MenuItem("Package Path")) ImGui.SetClipboardText(component.Actor?.PackagePath);
                if (ImGui.MenuItem("Object Path")) ImGui.SetClipboardText(component.ObjectPath);
                ImGui.EndMenu();
            }

            ImGui.Separator();
            if (ImGui.MenuItem($"{Settings.AddIcon}  Add Child"))
            {

            }
            ImGui.Separator();

            if (ImGui.MenuItem("\uf56e  Export"))
            {

            }
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
            if (ImGui.MenuItem($"{Settings.TrashIcon}  Delete"))
            {
                component.Actor?.Components.Remove(component);
                _dirty = true;
            }
            ImGui.PopStyleColor();

            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();

        if (ImGui.IsItemHovered() && ImGui.BeginTooltip())
        {
            if (warn)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0f, 1f));
                ImGui.TextUnformatted($"{WarnIcon}  Orphaned — not attached to the component tree.");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }

            var typeName = component.GetType().Name;
            if (ImGui.BeginTable("##CompTooltipMeta", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInner))
            {
                MetaRow("Type", typeName);
                MetaRow("Class", component.ExportType ?? "N/A");
                ImGui.EndTable();
            }

            var lineH = ImGui.GetFrameHeight() * 0.1f;
            var wPos = ImGui.GetWindowPos();
            var wSize = ImGui.GetWindowSize();

            var color = typeName == component.ExportType
                ? new Vector4(0.35f, 0.65f, 1f,  1f)
                : new Vector4(0.55f, 0.55f, 0.55f, 0.6f);

            var solid = ImGui.ColorConvertFloat4ToU32(color);
            var fade = ImGui.ColorConvertFloat4ToU32(color with { W = 0f });
            ImGui.GetForegroundDrawList().AddRectFilledMultiColor(wPos, new Vector2(wPos.X + wSize.X, wPos.Y + lineH), solid, fade, fade, solid);

            ImGui.EndTooltip();
        }

        // TODO: drag and drop

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !toggledOpen && component.Actor?.ActorManager is InterfaceManager manager)
        {
            manager.SelectComponent(component, scrollTo: false);
        }

        if (hasChildren)
        {
            if (toggledOpen) _dirty = true;
            if (nodeOpen) ImGui.TreePop();
        }

        var btnW = ImGui.CalcTextSize(FileIcon).X + style.FramePadding.X * 2;
        ImGui.SameLine(rightEdge - btnW);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        if (ImGui.Button(FileIcon)) component.FireJsonRequested();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        ImGui.PopID();
    }

    private static void MetaRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0); ImGui.TextDisabled(label);
        ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(value);
    }

    private static void BuildFlatList(Actor actor, bool isSearching, string search)
    {
        _flatNodes.Clear();
        _reachable.Clear();

        // Pass 1 – mark EVERY component reachable from the spatial tree,
        //          regardless of open/closed state (closed ≠ orphaned).
        foreach (var component in actor.Components)
        {
            _reachable.Add(component.Id);
        }

        // Pass 2 – populate the visible flat list from the spatial tree.
        if (actor.RootComponent != null)
            BuildSpatialNodes(actor.RootComponent, actor.Id, 0, isSearching, search);

        // Pass 3 – any component in actor.Components that was NOT reached by
        //          the tree traversal is either an orphaned spatial (warn) or
        //          a non-spatial leaf (no warn).
        foreach (var component in actor.Components)
        {
            if (_reachable.Contains(component.Id)) continue;

            var matches = !isSearching || component.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (!matches) continue;

            component.Depth = 0;
            component.Index = _flatNodes.Count;
            _flatNodes.Add(new FlatEntry(component, component is SpatialComponent));
        }
    }

    /// <summary>
    /// Recursively adds visible spatial nodes to the flat list.
    /// Children are added only when the parent node is open (or a search is active).
    /// </summary>
    private static void BuildSpatialNodes(SpatialComponent component, int actorId, int depth, bool isSearching, string search)
    {
        component.Depth = depth;

        var matches = !isSearching || component.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
        if (matches)
        {
            component.Index = _flatNodes.Count;
            _flatNodes.Add(new FlatEntry(component, false));
        }

        var hasChildren = component.Children.Any(c => c.Actor?.Id == actorId);
        if (hasChildren && (isSearching || component.Open))
        {
            foreach (var child in component.Children)
            {
                if (child.Actor?.Id != actorId) continue;
                BuildSpatialNodes(child, actorId, depth + 1, isSearching, search);
            }
        }
    }

    private static ActorComponent? FindScrollTarget(Actor actor)
    {
        if (actor.RootComponent != null)
        {
            var result = FindScrollTargetInTree(actor.RootComponent, actor.Id);
            if (result != null) return result;
        }

        foreach (var component in actor.Components)
        {
            if (component.ScrollToMe) return component;
        }

        return null;
    }

    private static ActorComponent? FindScrollTargetInTree(SpatialComponent component, int actorId)
    {
        if (component.ScrollToMe) return component;

        foreach (var child in component.Children)
        {
            if (child.Actor?.Id != actorId) continue;
            var result = FindScrollTargetInTree(child, actorId);
            if (result != null) return result;
        }

        return null;
    }
}
