using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Transforms;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;
    
    public readonly string Name;
    protected readonly string Header;
    private readonly string? _exportType;
    private readonly string? _internalType;

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
    
    internal virtual string Icon => "component";

    protected ActorComponent(string? name = null, string? exportType = null, string? internalType = null)
    {
        Name = name ?? "Unnamed";
        Header = UpperCaseToSpace().Replace(GetType().Name[..^"Component".Length], " $1");
        
        _exportType = exportType;
        _internalType = internalType;
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
            if (_actor == value) return;

            if (_actor == null)
                OnReworkThis();
            
            _actor = value;
            
            if (this is SpatialComponent { Relation: null } spatial)
            {
                spatial.Relation = _actor?.RootComponent;
            }
        }
    }
    
    public bool IsDirty { get; private set; }
    internal virtual void MarkDirty() => IsDirty = true; // spatial components will override this to propagate to children
    internal virtual void MarkClean() => IsDirty = false;

    protected virtual void OnReworkThis()
    {
        // TODO: this is used to start parsing materials asynchronously in MeshComponent
        // it is triggered by the component being assigned to an actor for "the first time"
        // but this is not a great way to handle materials specifically and it can be retriggered if the component is moved between actors
    }
    
    internal void DrawInterface()
    {
        if (this is not IControllable controllable) return;

        ImGui.PushID((int)Id);

        var condition = false;
        if (_exportType != null)
        {
            ImGui.Text($"Export Type: {_exportType}");
            condition = true;
        }
        if (_internalType != null)
        {
            ImGui.Text($"Internal Type: {_internalType}");
            condition = true;
        }
        if (condition)
        {
            ImGui.Spacing();
        }
        
        controllable.DrawControls();
        
        ImGui.PopID();
    }
    
    [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
    private partial System.Text.RegularExpressions.Regex UpperCaseToSpace();
}
