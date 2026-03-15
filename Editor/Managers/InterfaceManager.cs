using System.Numerics;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Editor.Managers;

public abstract class InterfaceManager(GameWindow wnd) : ImGuiManager(wnd)
{
    protected Actor? SelectedActor { get; private set; }
    protected ActorComponent? SelectedComponent { get; private set; }

    protected void SelectActor(Actor? actor) => Select(actor, null);
    protected void SelectComponent(ActorComponent? component) => Select(null, component);

    private void Select(Actor? actor, ActorComponent? component)
    {
        if (SelectedActor == actor && SelectedComponent == component) return;

        SelectedActor?.IsSelected = false;
        if (SelectedComponent is not null)
        {
            SelectedComponent.IsSelected = false;
            SelectedComponent.OnJsonRequested -= OnComponentJsonRequested;
            SelectedComponent.Actor?._isSelected = false;
        }

        SelectedActor = actor;
        SelectedComponent = component;

        if (SelectedActor is not null)
        {
            Log.Debug("Selected Actor: {ActorName}", SelectedActor.Name);
            SelectedActor.IsSelected = true;
        }
        if (SelectedComponent is not null)
        {
            Log.Debug("Selected Component ID: {ComponentId}", SelectedComponent.Id);
            SelectedComponent.IsSelected = true;
            SelectedComponent.OnJsonRequested += OnComponentJsonRequested;
            SelectedComponent.Actor?._isSelected = true;
        }

        OnSelectionChanged(SelectedActor, SelectedComponent);
    }

    protected sealed override void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        SelectComponent(GetComponentById(GetComponentId(mousePos, windowPos, windowSize)));
    }

    protected virtual void OnSelectionChanged(Actor? actor, ActorComponent? component)
    {

    }

    protected virtual void OnComponentJsonRequested(ActorComponent component, string[] properties)
    {

    }
}
