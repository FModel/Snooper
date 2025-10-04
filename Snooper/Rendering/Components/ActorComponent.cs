using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.Utils;
using ImGuiNET;
using Snooper.Rendering.Actors;
using Snooper.UI;

namespace Snooper.Rendering.Components;

public abstract partial class ActorComponent
{
    private static uint _nextId = 1;
    public readonly uint Id = _nextId++;
    
    public readonly string Name;
    protected readonly string? ExportType;
    protected readonly string? InternalType;
    protected readonly string Header;

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

    protected ActorComponent(string? name = null, string? exportType = null, string? internalType = null)
    {
        Name = name ?? "Unnamed";
        ExportType = exportType;
        InternalType = internalType;
        Header = UpperCaseToSpace().Replace(GetType().Name[..^"Component".Length], " $1");
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
        if (ExportType != null)
        {
            ImGui.Text($"Export Type: {ExportType}");
            condition = true;
        }
        if (InternalType != null)
        {
            ImGui.Text($"Internal Type: {InternalType}");
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
