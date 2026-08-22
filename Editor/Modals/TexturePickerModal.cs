using System.Numerics;
using ImGuiNET;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Cache;
using Snooper.UI;

namespace Editor.Modals;

/// <summary>Thumbnail grid over every texture the scene has uploaded.</summary>
public sealed class TexturePickerModal : AssetPickerModal<Texture>
{
    public static TexturePickerModal Instance { get; } = new();

    private const float TileSize = 88.0f;

    protected override string Title => "Select Texture";
    protected override string ItemNoun => "texture";

    protected override IEnumerable<Texture> Enumerate() => TextureCache.GetLoaded();
    protected override string NameOf(Texture item) => item.Name;

    protected override Texture? DrawItems(IReadOnlyList<Texture> items)
    {
        Texture? picked = null;

        var style = ImGui.GetStyle();
        var labelHeight = ImGui.GetTextLineHeight();
        var tileHeight = TileSize + labelHeight + style.ItemSpacing.Y;

        var columns = Math.Max(1, (int) (ImGui.GetContentRegionAvail().X / (TileSize + style.ItemSpacing.X)));
        var rows = (items.Count + columns - 1) / columns;

        // the cache can hold thousands of textures, so clip by row rather than submitting every tile
        unsafe
        {
            var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            clipper.Begin(rows, tileHeight + style.ItemSpacing.Y);
            while (clipper.Step())
            {
                for (var row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var index = row * columns + column;
                        if (index >= items.Count) break;

                        if (column > 0) ImGui.SameLine();
                        if (DrawTile(items[index], index, tileHeight, labelHeight)) picked = items[index];
                    }
                }
            }
            clipper.End();
            clipper.Destroy();
        }

        return picked;
    }

    private static bool DrawTile(Texture texture, int index, float tileHeight, float labelHeight)
    {
        ImGui.PushID(index);

        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(TileSize, tileHeight);

        var clicked = ImGui.InvisibleButton("##Tile", size);
        var hovered = ImGui.IsItemHovered();

        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            drawList.AddRectFilled(origin, origin + size, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
        }

        drawList.AddImage(texture.GetPointer(), origin, origin + new Vector2(TileSize));
        drawList.AddRect(origin, origin + new Vector2(TileSize), ImGui.GetColorU32(ImGuiCol.Border));

        // the name sits under the thumbnail, clipped rather than wrapped so every tile stays the same height
        var labelPos = origin + new Vector2(0.0f, TileSize + ImGui.GetStyle().ItemSpacing.Y);
        drawList.PushClipRect(labelPos, labelPos + new Vector2(TileSize, labelHeight), true);
        drawList.AddText(labelPos, ImGui.GetColorU32(ImGuiCol.Text), texture.Name);
        drawList.PopClipRect();

        if (hovered)
        {
            EditorUI.Tooltip($"{texture.Name}\n{texture.Width}x{texture.Height}, {texture.FormatName}, {texture.GetFormattedSpace()}");
        }

        ImGui.PopID();
        return clicked;
    }
}
