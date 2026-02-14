using System.Collections.Specialized;
using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Managers;
using Snooper.UI.Widgets;

namespace Snooper.UI.Systems;

public class EditorSystem : InterfaceSystem
{
    private readonly List<IWidget> _widgets = [];

    private Viewport? _mainViewport;

    public EditorSystem(GameWindow wnd) : base(wnd)
    {
        Viewports.CollectionChanged += OnViewportsCollectionChanged;

        _widgets.Add(new LogsViewer());
#if DEBUG
        _widgets.Add(new ImGuiDemo());
#endif
    }

    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();

        if (ImGui.Begin("Render Settings"))
        {
            if (_mainViewport is null)
            {
                EditorUI.CenteredErrorText("No viewport selected");
            }
            else
            {
                DrawControls();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Outliner"))
        {
            if (RootActor is { } root && root.Children.ToList() is { Count: > 0 } children)
            {
                foreach (var child in children)
                    DrawActorTree(child, true);
            }
        }
        ImGui.End();

        if (ImGui.Begin("Inspector"))
        {
            DrawActorInspector();
        }
        ImGui.End();

        foreach (var widget in _widgets)
        {
            widget.Render();
        }
    }

    private bool _scrollToSelected;
    private readonly Vector2 _iconSize = new(18);
    private readonly Vector2 _actionIconSize = new(16);

    private bool HasSelectedDescendant(Actor actor)
    {
        if (actor.IsSelected) return true;
        foreach (var child in actor.Children)
            if (HasSelectedDescendant(child)) return true;
        return false;
    }

    private void DrawActorTree(Actor actor, bool clip = false)
    {
        ImGui.PushID(actor.Id);

        var count = actor.Children.Count;
        var flags = ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap | ImGuiTreeNodeFlags.FramePadding;
        if (actor.IsSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (count == 0) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        var anyChildSelected = _scrollToSelected && HasSelectedDescendant(actor);
        if (anyChildSelected && count > 0) ImGui.SetNextItemOpen(true);

        var open = ImGui.TreeNodeEx("##Tree", flags);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            SelectedActor = actor;
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && MainViewport?.Camera is { } camera && actor.RootComponent is not null)
        {
            var (center, distance) = actor.RootComponent.GetTeleportPosition(camera);
            camera.TeleportTo(center + camera.Forward * distance);
        }

        ImGui.SameLine();
        if (Icons.TryGetValue(actor.Icon, out var icon))
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.Image(icon.GetPointer(), _iconSize);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
        }
        else
        {
            ImGui.Dummy(_iconSize);
        }

        ImGui.SameLine();
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiSemiBold]);
        ImGui.PushStyleColor(ImGuiCol.Text, actor.IsVisible ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted(actor.Name);
        ImGui.PopStyleColor();
        ImGui.PopFont();

        DrawActorActionButtons(actor);

        if (open && count > 0 && actor.Children.ToList() is { Count: > 0 } children)
        {
            var anyChildExpanded = false;
            if (clip && !anyChildSelected)
            {
                foreach (var child in children)
                {
                    ImGui.PushID(child.Id);
                    var isOpen = ImGui.GetStateStorage().GetInt(ImGui.GetID("##Tree")) != 0;
                    ImGui.PopID();
                    if (isOpen && child.Children.Count > 0)
                    {
                        anyChildExpanded = true;
                        break;
                    }
                }
            }

            // only use clipper if no child is expanded and not scrolling to selected
            if (clip && !anyChildSelected && !anyChildExpanded)
            {
                unsafe
                {
                    var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                    clipper.Begin(children.Count);
                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            DrawActorTree(children[i]);
                        }
                    }
                    clipper.End();
                    clipper.Destroy();
                }
            }
            else foreach (var child in children)
            {
                DrawActorTree(child);
            }

            ImGui.TreePop();
        }

        if (actor.IsSelected && _scrollToSelected)
        {
            ImGui.SetScrollHereY();
            _scrollToSelected = false;
        }

        ImGui.PopID();
    }

    private void DrawActorActionButtons(Actor actor)
    {
        var actionButtons = new List<(string id, string icon, string tooltip, Action action, bool enabled)>
        {
            ("visibility", actor.IsVisible ? "eye" : "eye_closed", actor.IsVisible ? "Hide" : "Show", () => actor.IsVisible = !actor.IsVisible, true),
            ("delete", "trash", "Delete", () =>
            {
                actor.Parent?.Children.Remove(actor);
                if (actor.IsSelected) SelectedActor = null;
            }, true),
        };

        if (actor is CellActor cell)
        {
            actionButtons.Insert(0, ("download", cell.IsLoaded ? "download_off" : "download", "Load", () => cell.EnqueueLoad(), cell is { CanLoad: true, IsLoaded: false, IsLoading: false }));
        }

        switch (actor.RootComponent)
        {
            case SceneCameraComponent { IsActive: true }:
                actionButtons.RemoveRange(0, 2);
                break;
        }

        if (actionButtons.Count == 0) return;

        var style = ImGui.GetStyle();
        var buttonX = ImGui.GetWindowWidth() - style.FramePadding.X - style.WindowPadding.X;
        buttonX -= ImGui.GetScrollMaxY() > 0 ? style.ScrollbarSize : 0;
        buttonX -= actionButtons.Count * (_actionIconSize.X + style.ItemSpacing.X) - style.ItemSpacing.X / 2;

        ImGui.SameLine();
        ImGui.SetCursorPosX(buttonX);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.3f, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.4f, 0.4f, 0.5f));

        var buttonY = ImGui.GetCursorPosY();
        for (var i = 0; i < actionButtons.Count; i++)
        {
            ImGui.SetCursorPosY(buttonY);

            var (id, iconName, tooltip, action, enabled) = actionButtons[i];
            ImGui.BeginDisabled(!enabled);
            if (Icons.TryGetValue(iconName, out var buttonIcon))
            {
                if (ImGui.ImageButton(id, buttonIcon.GetPointer(), _actionIconSize))
                {
                    action();
                }
            }
            else
            {
                if (ImGui.SmallButton(id[0].ToString().ToUpper()))
                {
                    action();
                }
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }

            if (i < actionButtons.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();
    }

    private void DrawActorInspector()
    {
        if (SelectedComponent is not { } component)
        {
            component = SelectedActor?.RootComponent;
            if (component is null)
            {
                ImGui.TextUnformatted("No actor or component selected.");
                return;
            }
        }

        if (component.Actor is not { } actor)
        {
            ImGui.TextUnformatted("This component is not assigned to any actor.");
            return;
        }

        actor.DrawInterface();

        var components = actor.Components;
        if (components.Count == 0)
        {
            ImGui.TextUnformatted("This actor has no components.");
            return;
        }

        ImGui.SeparatorText($"{components.Count} Component{(components.Count > 1 ? "s" : "")}");

        if (Icons.TryGetValue(component.Icon, out var icon))
            ImGui.Image(icon.GetPointer(), _iconSize);
        else
            ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
        ImGui.SameLine();ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.BeginCombo("##Components", component.Name))
        {
            foreach (var c in components)
            {
                if (Icons.TryGetValue(c.Icon, out icon))
                    ImGui.Image(icon.GetPointer(), _iconSize * 0.75f);
                else
                    ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
                ImGui.SameLine();

                var selected = c.Id == SelectedComponent?.Id;
                if (ImGui.Selectable(c.Name, selected))
                {
                    SelectedComponent = c;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        component.DrawInterface();
    }

    protected override void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        base.OnViewportLeftClick(mousePos, windowPos, windowSize);

        _scrollToSelected = true;
        ImGui.SetWindowFocus("Outliner");
    }

    private void OnViewportsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var viewport in e.NewItems!.Cast<Viewport>())
                {
                    viewport.OnLeftClick += OnViewportLeftClick;

                    _widgets.Add(viewport);
                    _mainViewport ??= viewport;
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var viewport in e.OldItems!.Cast<Viewport>())
                {
                    viewport.OnLeftClick -= OnViewportLeftClick;

                    _widgets.Remove(viewport);
                    if (_mainViewport == viewport)
                    {
                        _mainViewport = Viewports.FirstOrDefault();
                    }
                }
                break;
        }
    }
}
