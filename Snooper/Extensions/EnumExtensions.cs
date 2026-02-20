using System.ComponentModel;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Versions;

namespace Snooper.Extensions;

public static class EnumExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetDescription(this Enum value)
    {
        var fi = value.GetType().GetField(value.ToString());
        if (fi == null) return $"{value} ({value:D})";

        var attributes = (DescriptionAttribute[]) fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (attributes.Length > 0) return attributes[0].Description;


        var suffix = $"{value:D}";
        var current = Convert.ToInt32(suffix);
        var mask = value.GetType() == typeof(EGame) ? ~0xFFFF : ~0xF;
        var target = current & mask;
        if (current != target)
        {
            var values = Enum.GetValues(value.GetType());
            var index = Array.IndexOf(values, value);
            suffix = values.GetValue(index - (current - target))?.ToString();
        }
        return $"{value} ({suffix})";
    }
}
