using System.Numerics;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Editor.Managers;

public abstract class InterfaceManager(GameWindow wnd) : ImGuiManager(wnd)
{
    public Actor? SelectedActor { get; private set; }
    public ActorComponent? SelectedComponent { get; private set; }

    public void SelectActor(Actor? actor, bool scrollTo = true)
    {
        if (SelectedActor == actor && SelectedComponent == null) return;

        Deselect();

        SelectedActor = actor;
        if (actor == null) return;

        actor.IsNodeSelected = true;
        if (scrollTo) actor.ShouldScrollHere = true;

        // select and scroll to the root component so the inspector tree highlights it.
        if (actor.RootComponent is { } root)
        {
            root.IsNodeSelected = true;
            if (scrollTo) root.ShouldScrollHere = true;
        }

        actor.SetOutlined(true);

        Log.Debug("Selected Actor: {ActorName}", actor.Name);
        OnSelectionChanged(SelectedActor, SelectedComponent);
    }

    public void SelectComponent(ActorComponent? component, bool scrollTo = true)
    {
        if (SelectedComponent == component && SelectedActor == null) return;

        Deselect();

        SelectedComponent = component;
        if (component == null) return;

        component.IsNodeSelected = true;
        if (scrollTo) component.ShouldScrollHere = true;
        component.SetOutlined(true);

        // select the owning actor so the scene hierarchy highlights it.
        if (component.Actor is { } actor)
        {
            actor.IsNodeSelected = true;
            if (scrollTo) actor.ShouldScrollHere = true;
        }

        Log.Debug("Selected Component ID: {ComponentId}", component.Id);
        OnSelectionChanged(SelectedActor, SelectedComponent);
    }

    private void Deselect()
    {
        if (SelectedActor != null)
        {
            SelectedActor.IsNodeSelected = false;
            SelectedActor.ShouldScrollHere = false;

            if (SelectedActor.RootComponent is { } root)
            {
                root.IsNodeSelected = false;
                root.ShouldScrollHere = false;
            }

            SelectedActor.SetOutlined(false);
            SelectedActor = null;
        }

        if (SelectedComponent != null)
        {
            SelectedComponent.IsNodeSelected = false;
            SelectedComponent.ShouldScrollHere = false;
            SelectedComponent.SetOutlined(false);

            // actor was marked selected by SelectComponent
            if (SelectedComponent.Actor is { } actor)
            {
                actor.IsNodeSelected = false;
                actor.ShouldScrollHere = false;
            }

            SelectedComponent = null;
        }
    }

    public override void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        SelectComponent(GetComponentById(GetComponentId(mousePos, windowPos, windowSize)));
    }

    protected virtual void OnSelectionChanged(Actor? actor, ActorComponent? component)
    {

    }
}
