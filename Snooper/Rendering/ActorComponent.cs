using ImGuiNET;
using Snooper.UI;

namespace Snooper.Rendering;

public abstract partial class ActorComponent
{
    public Actor? Actor;
    
    private string? _displayName;
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
