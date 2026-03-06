using System.Numerics;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Editor.Managers;

public abstract class InterfaceManager(GameWindow wnd) : ImGuiManager(wnd)
{
    private Actor? _selectedActor;
    protected Actor? SelectedActor
    {
        get => _selectedActor;
        set
        {
            if (_selectedActor == value)
                return;

            ClearSelection();

            _selectedActor = value;
            _selectedComponent = null;
            if (_selectedActor is not null)
            {
                Log.Debug("Selected Actor: {ActorName}", _selectedActor.Name);
                _selectedActor.IsSelected = true;
            }
        }
    }

    private ActorComponent? _selectedComponent;
    protected ActorComponent? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (_selectedComponent == value)
                return;

            ClearSelection();

            _selectedComponent = value;
            _selectedActor = _selectedComponent?.Actor;
            if (_selectedComponent is not null)
            {
                Log.Debug("Selected Component ID: {ComponentId}", _selectedComponent.Id);
                _selectedComponent.IsSelected = true;
                _selectedComponent.OnJsonRequested += OnComponentJsonRequested;
                _selectedComponent.Actor?._isSelected = true; // mark actor as selected but don't outline all its components
            }
        }
    }

    protected override void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        SelectedActor = null;
        SelectedComponent = GetComponentById(GetComponentId(mousePos, windowPos, windowSize));
    }

    protected virtual void OnComponentJsonRequested(ActorComponent component, string[] properties)
    {

    }

    private void ClearSelection()
    {
        _selectedActor?.IsSelected = false;

        if (_selectedComponent is not null)
        {
            _selectedComponent.IsSelected = false;
            _selectedComponent.OnJsonRequested -= OnComponentJsonRequested;
            _selectedComponent.Actor?._isSelected = false;
        }
    }
}
