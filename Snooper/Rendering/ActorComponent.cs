using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.UI;

namespace Snooper.Rendering;

public abstract partial class ActorComponent(string? name = null)
{
    protected ActorComponent(UActorComponent component) : this($"{component.Name} ({component.GetType().Name})")
    {
        
    }
    
    private Actor? _actor;
    public Actor? Actor
    {
        get => _actor;
        internal set
        {
            if (_actor == value || value is null)
                return;
            
            var old = _actor;
            _actor = value;
            
            if (old == null) OnAddedToActor();
        }
    }
    
    private string? _displayName = name;
    public string DisplayName
    {
        get
        {
            if (_displayName is null)
            {
                var type = GetType().Name[..^"Component".Length];
                _displayName = UpperCaseToSpace().Replace(type, " $1");
            }
            
            return _displayName;
        }
    }
    
    public bool IsDirty { get; private set; }
    internal virtual void MarkDirty() => IsDirty = true; // spatial components will override this to propagate to children
    internal virtual void MarkClean() => IsDirty = false;
    
    protected virtual void OnAddedToActor() { }
    
    internal void DrawInterface()
    {
        if (this is not IControllable controllable || this is DebugComponent) return;
        
        if (ImGui.CollapsingHeader(DisplayName))
        {
            controllable.DrawControls();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private static partial System.Text.RegularExpressions.Regex UpperCaseToSpace();
}
