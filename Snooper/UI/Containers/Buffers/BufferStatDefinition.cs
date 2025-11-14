using System.Numerics;

namespace Snooper.UI.Containers.Buffers;

public class BufferStatDefinition(string label, string value, string? extraValue = null, Vector4? color = null, float minWidth = 0)
{
    public readonly ImGuiMeasuredText Label = new(label);
    public readonly ImGuiMeasuredText Value = new(value);
    public readonly ImGuiMeasuredText LongValue = new(extraValue != null ? value + extraValue : value);
    public readonly Vector4? Color = color;
    public readonly float MinWidth = minWidth;
        
    public bool UseLongVersion { get; internal set; }
}