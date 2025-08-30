using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.UI;

namespace Snooper.Rendering;

public abstract partial class ActorComponent(string? name = null)
{
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
    
    protected virtual void OnAddedToActor() { }

    internal void DrawInterface()
    {
        if (this is not IControllable controllable) return;
        
        if (ImGui.CollapsingHeader($"{DisplayName} Controls"))
        {
            controllable.DrawControls();
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private static partial System.Text.RegularExpressions.Regex UpperCaseToSpace();
}
