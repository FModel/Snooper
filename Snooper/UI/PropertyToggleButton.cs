using System.Numerics;

namespace Snooper.UI;

public readonly struct PropertyToggleButton(Func<string> icon, Action onClick, Func<string>? tooltip = null, Func<bool>? enabled = null, Func<Vector4?>? textColor = null)
{
    public readonly Func<string> Icon = icon;
    public readonly Func<string>? Tooltip = tooltip;
    public readonly Action OnClick = onClick;
    public readonly Func<bool>? Enabled = enabled;
    public readonly Func<Vector4?>? TextColor = textColor;
}
