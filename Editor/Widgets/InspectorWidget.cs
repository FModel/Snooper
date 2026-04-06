using Editor.Managers;
using ImGuiNET;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Transforms;
using System.Numerics;
using Snooper.Rendering.Actors;
using Serilog;
using Snooper;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

public class InspectorWidget
{
    private const string Title      = "Inspector";
    private const string WarnIcon   = "\uf071";
    private const string FileIcon   = "\uf1c9";
    private const string SearchIcon = "\uf002";

    private readonly record struct FlatEntry(ActorComponent Component, bool Warn);

    private int    _lastActorId        = -1;
    private int    _lastComponentCount = -1;
    private string _search             = "";
    private bool   _dirty              = true;

    private readonly List<FlatEntry>  _flatNodes = [];
    private readonly HashSet<int>     _reachable = [];

    public void Draw(Actor? selectedActor, ActorComponent? selectedComponent)
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

            ImGui.SeparatorText($"{actor.Name} ({actor.Class ?? "N/A"} - {componentCount} Component{(componentCount != 1 ? "s" : "")})");
            DrawClippedTree(actor);

            (selectedComponent ?? actor.RootComponent)?.DrawControls();

            ImGui.SeparatorText("");
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.Header));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            var width = ImGui.GetContentRegionAvail().X;
            if (ImGui.Button($"{Settings.AddIcon}  Add Component", new Vector2(width, 0)))
            {

            }
            ImGui.PopStyleColor(3);
        }
        ImGui.End();
    }

    private void DrawSearchBar()
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

    private void DrawClippedTree(Actor actor)
    {
        if (actor.Components.Count == 0)
        {
            ImGui.TextUnformatted("No components.");
            return;
        }

        ActorComponent? scrollTarget = null;
        if (actor.Components.Any(c => c.ShouldScrollHere))
        {
            scrollTarget = FindScrollTarget(actor);
            if (scrollTarget != null)
            {
                _dirty = true;
                scrollTarget.ShouldScrollHere = false;
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
            if (scrollTarget is { NodeIndex: >= 0 })
            {
                var itemY = scrollTarget.NodeIndex * frameH;
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

    private void DrawFlatNode(ActorComponent component, bool warn, bool isSearching)
    {
        ImGui.PushID(component.Id);

        var style = ImGui.GetStyle();
        var indent = isSearching ? 0f : component.NodeDepth * style.IndentSpacing * 0.5f;
        ImGui.SetCursorPosX(style.WindowPadding.X + indent);
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;

        var hasChildren = !isSearching
                          && component is SpatialComponent sp
                          && sp.Children.Any(c => c.Actor?.Id == component.Actor?.Id);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                    ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding;
        if (component.IsNodeSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        else ImGui.SetNextItemOpen(component.IsNodeOpen, ImGuiCond.Always);

        if (warn) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0f, 1f));
        var nodeOpen = ImGui.TreeNodeEx("##Component", flags, $"{(warn ? $"{WarnIcon}  " : "")}{component.Icon}  {component.Name}");
        component.IsNodeOpen = nodeOpen;
        if (warn) ImGui.PopStyleColor();

        var toggledOpen = ImGui.IsItemToggledOpen();

        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1f);
        if (ImGui.BeginPopupContextItem("##ComponentContext"))
        {
            ImGui.TextDisabled(component.Name);
            ImGui.Separator();

            if (component is SkeletalMeshComponent sk && ImGui.MenuItem("\uf04b  Set Animation"))
            {
                sk.SetAnimation(null); // TODO
            }
            if (ImGui.MenuItem("\uf124  Teleport To") && component is SpatialComponent spatial) spatial.TeleportTo();
            if (ImGui.MenuItem("\uf1c9  Open JSON"))
            {
                if (component.Actor?.ActorManager is EditorManager manager)
                    manager._jsonViewer.Open(component);
            }
            if (ImGui.MenuItem("\uf24d  Clone")) component.Actor?.Components.Add((ActorComponent) component.Clone());
            if (ImGui.BeginMenu("\uf0c5  Copy"))
            {
                if (ImGui.MenuItem("Package Path")) ImGui.SetClipboardText(component.OwnerPath);
                if (ImGui.MenuItem("Object Path")) ImGui.SetClipboardText(component.Path);
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
            ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
            if (ImGui.MenuItem($"{Settings.TrashIcon}  Delete"))
            {
                component.Actor?.Components.Remove(component);
                _dirty = true;
            }
            ImGui.PopStyleColor();

            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();

        if (ImGui.IsItemHovered())
        {
            if (ImGui.BeginTooltip())
            {
                if (warn)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0f, 1f));
                    ImGui.TextUnformatted($"{WarnIcon}  Orphaned component not attached to the tree.");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                if (ImGui.BeginTable("##CompTooltipMeta", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInner))
                {
                    MetaRow("Type", component.Type);
                    MetaRow("Class", component.Class ?? "N/A");
                    ImGui.EndTable();
                }

                var lineH = ImGui.GetFrameHeight() * 0.1f;
                var wPos = ImGui.GetWindowPos();
                var wSize = ImGui.GetWindowSize();

                var color = component.Type == component.Class
                    ? new Vector4(0.35f, 0.65f, 1f,  1f)
                    : new Vector4(0.55f, 0.55f, 0.55f, 0.6f);

                var solid = ImGui.ColorConvertFloat4ToU32(color);
                var fade = ImGui.ColorConvertFloat4ToU32(color with { W = 0f });
                ImGui.GetForegroundDrawList().AddRectFilledMultiColor(wPos, new Vector2(wPos.X + wSize.X, wPos.Y + lineH), solid, fade, fade, solid);

                ImGui.EndTooltip();
            }

            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && component is SpatialComponent spatial)
            {
                spatial.TeleportTo();
            }
        }

        // TODO: drag and drop

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !toggledOpen)
        {
            if (component.Actor?.ActorManager is InterfaceManager manager)
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
        if (ImGui.Button(FileIcon))
        {
            if (component.Actor?.ActorManager is EditorManager manager)
                manager._jsonViewer.Open(component);
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        ImGui.PopID();
    }

    private void MetaRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0); ImGui.TextDisabled(label);
        ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(value);
    }

    private void BuildFlatList(Actor actor, bool isSearching, string search)
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

            component.NodeDepth = 0;
            component.NodeIndex = _flatNodes.Count;
            _flatNodes.Add(new FlatEntry(component, component is SpatialComponent));
        }
    }

    /// <summary>
    /// Recursively adds visible spatial nodes to the flat list.
    /// Children are added only when the parent node is open (or a search is active).
    /// </summary>
    private void BuildSpatialNodes(SpatialComponent component, int actorId, int depth, bool isSearching, string search)
    {
        component.NodeDepth = depth;

        var matches = !isSearching || component.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
        if (matches)
        {
            component.NodeIndex = _flatNodes.Count;
            _flatNodes.Add(new FlatEntry(component, false));
        }

        var hasChildren = component.Children.Any(c => c.Actor?.Id == actorId);
        if (hasChildren && (isSearching || component.IsNodeOpen))
        {
            foreach (var child in component.Children)
            {
                if (child.Actor?.Id != actorId) continue;
                BuildSpatialNodes(child, actorId, depth + 1, isSearching, search);
            }
        }
    }

    private ActorComponent? FindScrollTarget(Actor actor)
    {
        if (actor.RootComponent != null)
        {
            var result = FindScrollTargetInTree(actor.RootComponent, actor.Id);
            if (result != null) return result;
        }

        foreach (var component in actor.Components)
        {
            if (component.ShouldScrollHere) return component;
        }

        return null;
    }

    private ActorComponent? FindScrollTargetInTree(SpatialComponent component, int actorId)
    {
        if (component.ShouldScrollHere) return component;

        foreach (var child in component.Children)
        {
            if (child.Actor?.Id != actorId) continue;
            var result = FindScrollTargetInTree(child, actorId);
            if (result != null) return result;
        }

        return null;
    }
}
