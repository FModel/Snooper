using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.UI;

namespace Editor.Widgets.Cache;

// the pieces the memory tabs are made of, in the look of the profiler card with the room of a full panel
internal static class MemoryChart
{
    public static float Unit => ImGui.GetFrameHeight();
    public static float Gap => 0.5f * Unit;
    public static float RowHeight => MathF.Round(1.3f * Unit);
    public static float BarHeight => MathF.Round(1.5f * Unit);
    public static float Square => 0.6f * Unit;
    public static float MeterWidth => 6f * Unit;
    public static float ValueWidth => 9.5f * Unit;
    public static float TilesHeight => ImGui.GetFontSize() * 2f + 0.2f * Unit;

    public static readonly uint NameColor = Color(0.85f, 0.85f, 0.88f);
    public static readonly uint ValueColor = Color(0.62f, 0.62f, 0.68f);
    public static readonly uint DimColor = Color(0.5f, 0.5f, 0.55f);
    public static readonly uint AccentColor = Color(0.30f, 0.55f, 0.95f);

    public readonly struct Tile(string label, string value, uint color = 0)
    {
        public readonly string Label = label;
        public readonly string Value = value;
        public readonly uint Color = color;
    }

    // share is its part of the bar; what the segments leave is drawn as the empty tail
    public readonly struct Segment(string label, uint color, float share)
    {
        public readonly string Label = label;
        public readonly uint Color = color;
        public readonly float Share = share;
    }

    public static bool Chip(string label, bool active)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.CalcTextSize(label).X + Gap * 2f, Unit);

        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();

        if (active || hovered) drawList.AddRectFilled(pos, pos + size, Color(1f, 1f, 1f, active ? 0.12f : 0.06f));
        drawList.AddText(pos + new Vector2(Gap, Center(Unit)), active ? Color(1f, 1f, 1f) : hovered ? NameColor : DimColor, label);

        return clicked;
    }

    public static void Tiles(ReadOnlySpan<Tile> tiles)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var column = MathF.Min(width / tiles.Length, 12f * Unit);
        var font = ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold];
        var fontSize = ImGui.GetFontSize();

        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var x = pos.X + i * column;

            drawList.AddText(new Vector2(x, pos.Y), DimColor, tile.Label);
            drawList.AddText(font, fontSize, new Vector2(x, pos.Y + fontSize + 0.2f * Unit), tile.Color != 0 ? tile.Color : NameColor, tile.Value);
        }

        ImGui.Dummy(new Vector2(width, TilesHeight));
    }

    public static void Bar(ReadOnlySpan<Segment> segments)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), BarHeight);

        drawList.AddRectFilled(pos, pos + size, Color(1f, 1f, 1f, 0.06f));

        var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pos, pos + size);
        var mouse = ImGui.GetMousePos().X;
        var x = pos.X;
        foreach (var segment in segments)
        {
            var end = MathF.Min(pos.X + size.X, x + size.X * segment.Share);
            if (end - x >= 1f)
            {
                drawList.AddRectFilled(new Vector2(x, pos.Y), new Vector2(end, pos.Y + size.Y), segment.Color);
                if (hovered && mouse >= x && mouse < end) EditorUI.Tooltip(segment.Label);
            }
            x = end;
        }

        if (hovered && mouse >= x) EditorUI.Tooltip("Unused");

        drawList.AddRect(pos, pos + size, Color(1f, 1f, 1f, 0.15f));
        ImGui.Dummy(size);
    }

    // the caller pushes an id per row, and no vertical item spacing around a list of them
    // a fraction at or above zero adds a meter before the value
    public static bool Row(uint color, string name, string value, float fraction = -1f, bool drillable = false, bool selected = false, bool enabled = true)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = RowHeight;
        var right = pos.X + width;
        var text = Center(height);

        var clicked = ImGui.InvisibleButton("##Row", new Vector2(width, height)) && enabled;
        var hovered = enabled && ImGui.IsItemHovered();

        if (selected)
        {
            drawList.AddRectFilled(pos, new Vector2(right, pos.Y + height), Fade(AccentColor, 0.18f));
            drawList.AddRectFilled(pos, new Vector2(pos.X + 0.15f * Unit, pos.Y + height), AccentColor);
        }
        else if (hovered)
        {
            drawList.AddRectFilled(pos, new Vector2(right, pos.Y + height), Color(1f, 1f, 1f, 0.06f));
        }

        var square = new Vector2(pos.X + Gap, pos.Y + (height - Square) * 0.5f);
        drawList.AddRectFilled(square, square + new Vector2(Square), color);

        var valueX = right - Gap - ImGui.CalcTextSize(value).X;
        drawList.AddText(new Vector2(valueX, pos.Y + text), ValueColor, value);

        var nameEnd = valueX - Gap;
        if (fraction >= 0f)
        {
            var meter = new Vector2(right - Gap - ValueWidth - MeterWidth, pos.Y + (height - 0.35f * Unit) * 0.5f);
            var meterSize = new Vector2(MeterWidth, 0.35f * Unit);

            drawList.AddRectFilled(meter, meter + meterSize, Color(1f, 1f, 1f, 0.08f));
            drawList.AddRectFilled(meter, new Vector2(meter.X + meterSize.X * Math.Clamp(fraction, 0f, 1f), meter.Y + meterSize.Y), color);
            nameEnd = meter.X - Gap;
        }

        var nameX = square.X + Square + Gap;
        drawList.PushClipRect(pos, new Vector2(nameEnd, pos.Y + height), true);
        drawList.AddText(new Vector2(nameX, pos.Y + text), enabled ? NameColor : DimColor, name);
        if (drillable) drawList.AddText(new Vector2(nameX + ImGui.CalcTextSize(name).X + Gap, pos.Y + text), DimColor, Settings.AngleRightIcon);
        drawList.PopClipRect();

        return clicked;
    }

    public static float Center(float height) => (height - ImGui.GetFontSize()) * 0.5f;

    public static uint Fade(uint color, float alpha) => (color & 0x00FFFFFF) | (uint) (alpha * 255f) << 24;

    public static uint Color(float r, float g, float b, float a = 1f) => (uint) (a * 255f) << 24 | (uint) (b * 255f) << 16 | (uint) (g * 255f) << 8 | (uint) (r * 255f);
}
