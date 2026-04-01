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

    public void SelectActor(Actor? actor) => Select(actor, null);
    public void SelectComponent(ActorComponent? component, bool scrollTo = true) => Select(null, component, scrollTo);

    private void Select(Actor? actor, ActorComponent? component, bool scrollTo = true)
    {
        if (SelectedActor == actor && SelectedComponent == component) return;

        SelectedActor?.Selected = false;
        SelectedActor?.IsOutlined = false;
        SelectedActor?.RootComponent?.OnJsonRequested -= OnComponentJsonRequested;

        if (SelectedComponent is not null)
        {
            SelectedComponent.Selected = false;
            SelectedComponent.OnJsonRequested -= OnComponentJsonRequested;
            SelectedComponent.Actor?.Selected = false;
            SelectedComponent.Actor?.IsOutlined = false;
        }

        SelectedActor = actor;
        SelectedComponent = component;

        if (SelectedActor is not null)
        {
            Log.Debug("Selected Actor: {ActorName}", SelectedActor.Name);
            SelectedActor.Selected = true;
            SelectedActor.IsOutlined = true;
            SelectedActor.RootComponent?.OnJsonRequested += OnComponentJsonRequested;
        }

        if (SelectedComponent is not null)
        {
            Log.Debug("Selected Component ID: {ComponentId}", SelectedComponent.Id);
            SelectedComponent.Selected = true;
            SelectedComponent.OnJsonRequested += OnComponentJsonRequested;
            SelectedComponent.Actor?.Selected = true;
        }

        if (SelectedComponent is not null && SelectedActor is null)
        {
            SelectedComponent.Actor?.ScrollToMe = true;
            if (scrollTo) SelectedComponent.ScrollToMe = true;
        }
        if (SelectedComponent is null && SelectedActor is not null)
        {
            SelectedActor.RootComponent?.ScrollToMe = true;
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
