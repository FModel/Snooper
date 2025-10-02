using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.Utils;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract class ActorComponent(string? name = null, string? @class = null, string? type = null)
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;
    
    public readonly string Name = name ?? "Unnamed";
    public readonly string? Class = @class;
    public readonly string? Type = type;

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
    
    protected ActorComponent(UActorComponent component) : this(component.Name, component.ExportType, component.GetType().Name)
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
    
    public bool IsDirty { get; private set; }
    internal virtual void MarkDirty() => IsDirty = true; // spatial components will override this to propagate to children
    internal virtual void MarkClean() => IsDirty = false;
    
    protected virtual void OnAddedToActor() { }
    
    internal void DrawInterface()
    {
        if (this is not IControllable controllable || this is DebugComponent) return;

        ImGui.PushID((int)Id);

        var condition = false;
        if (Class != null && "U" + Class != Type)
        {
            ImGui.Text($"Class: {Class}");
            condition = true;
        }
        if (Type != null && !("U" + Name).StartsWith(Type))
        {
            ImGui.Text($"Type: {Type}");
            condition = true;
        }
        if (condition)
        {
            ImGui.Spacing();
        }
        
        controllable.DrawControls();
        
        ImGui.PopID();
    }
}
