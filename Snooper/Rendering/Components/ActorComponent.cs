using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent(string? name = null)
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value)
                return;
            
            _isSelected = value;

            Actor?.ComputeSelected();
        }
    }
    
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

        ImGui.PushID((int)Id);
        
        if (IsSelected) // TODO: just an example
            ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 0.5f));
        
        if (ImGui.CollapsingHeader(DisplayName))
            controllable.DrawControls();
        
        if (IsSelected)
            ImGui.PopStyleColor();
        
        ImGui.PopID();
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private static partial System.Text.RegularExpressions.Regex UpperCaseToSpace();
}
