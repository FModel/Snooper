using ImGuiNET;
using Snooper.Rendering.Actors;
using System.Numerics;
using Editor.Managers;
using Serilog;
using Snooper;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

public class SceneHierarchyWidget
{
    private const string Title           = "Scene Hierarchy";
    private const string SearchIcon      = "\uf002";
    private const string CollapseAllIcon = "\uf066";

    private int _lastActorCount = -1;

    private string _newActorName = "";
    private Actor? _newActorParent;
    private bool _pendingAddModal;

    private string _search = "";
    private bool _dirty = true;
    private readonly List<Actor> _flatNodes = [];

    public void Draw(Actor? actor)
    {
        if (ImGui.Begin(Title))
        {
            DrawSearchBar();

            var actorCount = (actor?.ActorManager?.ActorCount ?? 1) - 1;
            if (actorCount != _lastActorCount)
            {
                _lastActorCount = actorCount;
                _dirty = true;
            }

            ImGui.SeparatorText($"{actor?.Name} ({actorCount} Actor{(actorCount > 1 ? "s" : "")})");
            DrawClippedTree(actor);

            if (_pendingAddModal)
            {
                ImGui.OpenPopup("New Actor");
                _pendingAddModal = false;
            }

            DrawAddModal(actor);
        }
        ImGui.End();
    }

    private void DrawSearchBar()
    {
        var style = ImGui.GetStyle();
        var addBtnWidth = ImGui.CalcTextSize(Settings.AddIcon).X + style.FramePadding.X * 2;
        var collapseBtnWidth = ImGui.CalcTextSize(CollapseAllIcon).X + style.FramePadding.X * 2;
        var iconWidth = ImGui.CalcTextSize(SearchIcon).X;
        var inputWidth = ImGui.GetContentRegionAvail().X - iconWidth - addBtnWidth - collapseBtnWidth - style.ItemSpacing.X * 2;

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(SearchIcon);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputTextWithHint("##ActorSearch", "Search...", ref _search, 128, ImGuiInputTextFlags.AutoSelectAll))
        {
            _dirty = true;
        }
        ImGui.SameLine();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
        if (ImGui.Button(Settings.AddIcon))
        {
            _newActorParent = null;
            _pendingAddModal = true;
        }
        ImGui.SameLine();
        if (ImGui.Button(CollapseAllIcon))
        {
            foreach (var a in _flatNodes)
            {
                a.IsNodeOpen = false;
            }
            _dirty = true;
        }
        ImGui.PopStyleVar();
    }

    private void DrawClippedTree(Actor? actor)
    {
        if (actor is not { Children: { Count: > 0 } children })
        {
            ImGui.TextUnformatted("No actors.");
            return;
        }

        Actor? scrollTarget = null;
        if (actor.ShouldScrollHere)
        {
            scrollTarget = FindScrollTarget(children);
            if (scrollTarget != null)
            {
                _dirty = true;
                scrollTarget.ShouldScrollHere = false;
                Log.Verbose("Found actor scroll target: {ActorName}", scrollTarget.Name);
            }
            else
            {
                Log.Warning("Actor scroll target not found in hierarchy");
                actor.ShouldScrollHere = false;
            }
        }

        var isSearching = !string.IsNullOrWhiteSpace(_search);
        if (_dirty)
        {
            _flatNodes.Clear();
            BuildFlatList(children, isSearching, _search);
            Log.Verbose("Rebuilt actor flat list with {Count} entries", _flatNodes.Count);
            _dirty = false;
        }

        if (ImGui.BeginChild("##ActorTreeScroll", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground))
        {
            var frameHeightWithSpacing = ImGui.GetFrameHeightWithSpacing();
            if (scrollTarget is { NodeIndex: >= 0 })
            {
                var itemY = scrollTarget.NodeIndex * frameHeightWithSpacing;
                var centered = itemY - ImGui.GetWindowHeight() * 0.5f + frameHeightWithSpacing * 0.5f;
                ImGui.SetScrollY(MathF.Max(0f, centered));
            }

            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_flatNodes.Count, frameHeightWithSpacing);
                while (clipper.Step())
                {
                    for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        DrawFlatNode(_flatNodes[i], isSearching);
                    }
                }

                clipper.End();
                clipper.Destroy();
            }
        }
        ImGui.EndChild();
    }

    private void DrawAddModal(Actor? actor)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowSize(new Vector2(viewport.Size.X * 0.2f, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("New Actor", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
        {
            if (ImGui.IsWindowAppearing())
            {
                _newActorName = "";
                ImGui.SetKeyboardFocusHere();
            }

            var target = _newActorParent ?? actor;
            ImGui.TextDisabled($"Child of {target?.Name}");
            ImGui.SetNextItemWidth(-1);
            var confirmed = ImGui.InputTextWithHint("##NewActorName", "Name...", ref _newActorName, 128, ImGuiInputTextFlags.EnterReturnsTrue);

            ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

            var btnSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f, 0);
            var canConfirm = !string.IsNullOrWhiteSpace(_newActorName);

            ImGui.BeginDisabled(!canConfirm);
            var close = ImGui.Button("OK", btnSize) || (confirmed && canConfirm);
            if (close)
            {
                target?.Children.Add(new Actor(_newActorName.Trim()));
                _dirty = true;
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            close |= ImGui.Button("Cancel", btnSize);

            if (close)
            {
                _newActorParent = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawFlatNode(Actor actor, bool isSearching = false)
    {
        ImGui.PushID(actor.Id);

        var style = ImGui.GetStyle();
        var indent = isSearching ? 0f : (actor.NodeDepth - 1) * style.IndentSpacing * 0.5f;
        ImGui.SetCursorPosX(style.WindowPadding.X + indent);
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;

        var hasChildren = actor.Children.Count > 0 && !isSearching;
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap |
                    ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding;
        if (actor.IsNodeSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        else ImGui.SetNextItemOpen(actor.IsNodeOpen, ImGuiCond.Always);

        actor.IsNodeOpen = ImGui.TreeNodeEx("##Actor", flags, $"{actor.Icon}  {actor.Name}");
        var toggledOpen = ImGui.IsItemToggledOpen();

        if (ImGui.BeginPopupContextItem("##ActorContext"))
        {
            ImGui.TextDisabled(actor.Name);
            ImGui.Separator();

            if (actor.RootComponent is SkeletalMeshComponent sk && ImGui.MenuItem("\uf04b  Set Animation"))
            {
                sk.SetAnimation(null); // TODO
            }
            if (ImGui.MenuItem($"{Settings.EyeIcon}  Toggle Visibility")) actor.ToggleVisibility();
            if (ImGui.MenuItem("\uf124  Teleport To")) actor.RootComponent?.TeleportTo();
            if (ImGui.MenuItem("\uf1c9  Open JSON"))
            {
                if (actor.ActorManager is EditorManager manager)
                    manager._jsonViewer.Open(actor);
            }
            if (ImGui.MenuItem("\uf24d  Clone")) actor.Parent?.Children.Add((Actor) actor.Clone());
            if (ImGui.BeginMenu("\uf0c5  Copy"))
            {
                if (ImGui.MenuItem("Package Path")) ImGui.SetClipboardText(actor.OwnerPath);
                if (ImGui.MenuItem("Object Path")) ImGui.SetClipboardText(actor.Path);
                ImGui.EndMenu();
            }

            ImGui.Separator();
            if (ImGui.MenuItem($"{Settings.AddIcon}  Add Child"))
            {
                _newActorParent = actor;
                _pendingAddModal = true;
            }
            ImGui.Separator();

            if (ImGui.MenuItem("\uf56e  Export"))
            {
                // foreach (var exporter in actor.GetExporters())
                // {
                //     if (exporter is null) continue;
                // }
            }
            ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
            if (ImGui.MenuItem($"{Settings.TrashIcon}  Delete"))
            {
                actor.Parent?.Children.Remove(actor);
                _dirty = true;
            }
            ImGui.PopStyleColor();

            ImGui.EndPopup();
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !toggledOpen)
        {
            if (actor.ActorManager is InterfaceManager manager)
                manager.SelectActor(actor, scrollTo: false);
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            actor.RootComponent?.TeleportTo();
        }

        if (hasChildren)
        {
            if (toggledOpen) _dirty = true;
            if (actor.IsNodeOpen) ImGui.TreePop();
        }

        var btnW = ImGui.CalcTextSize(Settings.EyeIcon).X + style.FramePadding.X * 2;
        ImGui.SameLine(rightEdge - btnW);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        var isHidden = !actor.IsVisible;
        if (isHidden) ImGui.PushStyleColor(ImGuiCol.Text, Settings.RedColor);
        if (ImGui.Button(actor.IsVisible ? Settings.EyeIcon : Settings.EyeSlashIcon)) actor.ToggleVisibility();
        ImGui.PopStyleColor(isHidden ? 2 : 1);
        ImGui.PopStyleVar();

        ImGui.PopID();
    }

    private void BuildFlatList(IEnumerable<Actor> actors, bool isSearching = false, string search = "")
    {
        foreach (var actor in actors)
        {
            var matches = !isSearching || actor.Name.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (matches)
            {
                actor.NodeIndex = _flatNodes.Count;
                _flatNodes.Add(actor);
            }

            if (actor.Children.Count > 0 && (isSearching || actor.IsNodeOpen))
                BuildFlatList(actor.Children, isSearching, search);
        }
    }

    private Actor? FindScrollTarget(IEnumerable<Actor> actors)
    {
        foreach (var actor in actors)
        {
            if (!actor.ShouldScrollHere) continue;
            if (actor.IsNodeSelected) return actor;

            if (FindScrollTarget(actor.Children) is { } found)
                return found;
        }
        return null;
    }
}
