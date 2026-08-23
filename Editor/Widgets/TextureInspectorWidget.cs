using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using Snooper;
using Snooper.Core.Containers.Textures;
using Snooper.UI;

namespace Editor.Widgets;

/// <summary>
/// Zoom/pan viewer for a single texture, opened by clicking any texture preview in the editor.
/// Laid out as header / toolbar / canvas / status bar. The canvas is a texel-aligned grid the image
/// sits on, so transparent regions read as empty space and the scale stays legible while zooming.
/// </summary>
public class TextureInspectorWidget : PanelWidget
{
    public override string PanelTitle => Settings.TextureInspectorWindow;
    public override PanelGroup Group => PanelGroup.Tools;

    public override bool IsOpen { get; set; } // this widget is opened on demand

    private const float MinZoom = 0.05f;
    private const float MaxZoom = 64.0f;

    /// <summary>Smallest on-screen spacing a grid line may have before the texel step doubles.</summary>
    private const float MinGridSpacing = 10.0f;
    private const int MajorGridEvery = 8;

    private static readonly Vector4 _redChannel = new(1.0f, 0.35f, 0.35f, 1.0f);
    private static readonly Vector4 _greenChannel = new(0.35f, 1.0f, 0.35f, 1.0f);
    private static readonly Vector4 _blueChannel = new(0.45f, 0.6f, 1.0f, 1.0f);
    private static readonly Vector4 _alphaChannel = new(0.8f, 0.8f, 0.8f, 1.0f);

    private Texture? _texture;
    private float _zoom = 1.0f;
    private Vector2 _pan;
    private bool _fitPending = true;

    private bool _red = true;
    private bool _green = true;
    private bool _blue = true;
    private bool _alpha;

    private Vector2? _hoveredTexel;

    protected override void DrawContents(EditorManager editor)
    {
        if (WindowRequests.GetPayload<Texture>(PanelTitle) is { } requested && requested.Guid != _texture?.Guid)
        {
            _texture = requested;
            _fitPending = true;
        }

        if (_texture is not { } texture)
        {
            ImGui.TextDisabled("Click a texture preview to inspect it.");
            return;
        }

        DrawHeader(texture);
        DrawToolbar();

        var footer = ImGui.GetFrameHeightWithSpacing();
        DrawCanvas(texture, ImGui.GetContentRegionAvail() - new Vector2(0, footer));
        DrawStatusBar(texture);
    }

    private static void DrawHeader(Texture texture)
    {
        ImGui.TextUnformatted(texture.Name);

        if (texture.IsSrgb)
        {
            ImGui.SameLine();
            ImGui.TextColored(Settings.OrangeColor, "sRGB");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This texture is gamma-corrected for display.");
            }
        }

        EditorUI.Caption($"{texture.Width}x{texture.Height}, {texture.FormatName}, {texture.GetFormattedSpace()}");

        ImGui.Separator();
    }

    private void DrawToolbar()
    {
        if (EditorUI.IconButton(Settings.FocusIcon, "Fit\nScale the image to the window")) _fitPending = true;

        ImGui.SameLine();
        if (EditorUI.IconButton("1:1", "Actual Size\nOne texel per pixel"))
        {
            _zoom = 1.0f;
            _pan = Vector2.Zero;
        }

        ImGui.SameLine();
        if (EditorUI.IconButton(Settings.CopyIcon, "Copy Name") && _texture is { } texture)
        {
            ImGui.SetClipboardText(texture.Name);
        }

        ImGui.SameLine();
        EditorUI.VerticalSeparator();
        ImGui.SameLine();

        ChannelToggle("R", ref _red, _redChannel);
        ImGui.SameLine();
        ChannelToggle("G", ref _green, _greenChannel);
        ImGui.SameLine();
        ChannelToggle("B", ref _blue, _blueChannel);
        ImGui.SameLine();
        ChannelToggle("A", ref _alpha, _alphaChannel, "blend", "ignore");

        ImGui.Separator();
    }

    private static void ChannelToggle(string channel, ref bool enabled, Vector4 color, string enable = "show", string disable = "hide")
    {
        var tint = enabled ? color : ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled];
        if (EditorUI.IconButton(channel, $"{channel} Channel\nClick to {(enabled ? disable : enable)} it", textColor: tint))
        {
            enabled = !enabled;
        }
    }

    private void DrawStatusBar(Texture texture)
    {
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        ImGui.TextUnformatted($"{_zoom * 100.0f:0.#}%");

        var readout = _hoveredTexel is { } texel ? $"{(int) texel.X}, {(int) texel.Y}" : $"{texture.Width}x{texture.Height}";
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(readout).X);
        ImGui.TextUnformatted(readout);

        ImGui.PopStyleColor();
    }

    private void DrawCanvas(Texture texture, Vector2 canvasSize)
    {
        _hoveredTexel = null;
        if (canvasSize.X < 1.0f || canvasSize.Y < 1.0f) return;

        var canvasMin = ImGui.GetCursorScreenPos();
        var canvasMax = canvasMin + canvasSize;
        var imageSize = new Vector2(texture.Width, texture.Height);

        if (_fitPending)
        {
            _zoom = Math.Clamp(MathF.Min(canvasSize.X / imageSize.X, canvasSize.Y / imageSize.Y), MinZoom, MaxZoom);
            _pan = Vector2.Zero;
            _fitPending = false;
        }

        ImGui.InvisibleButton("##TextureCanvas", canvasSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonMiddle);
        var hovered = ImGui.IsItemHovered();

        if (ImGui.IsItemActive() && (ImGui.IsMouseDragging(ImGuiMouseButton.Left) || ImGui.IsMouseDragging(ImGuiMouseButton.Middle)))
        {
            _pan += ImGui.GetIO().MouseDelta;
        }

        var center = canvasMin + canvasSize * 0.5f;

        if (hovered && ImGui.GetIO().MouseWheel is var wheel and not 0.0f)
        {
            // zoom about the cursor: keep whatever texel is under the mouse pinned there
            var previous = _zoom;
            _zoom = Math.Clamp(_zoom * MathF.Pow(1.15f, wheel), MinZoom, MaxZoom);

            var pivot = ImGui.GetIO().MousePos - center - _pan;
            _pan -= pivot * (_zoom / previous - 1.0f);
        }

        var scaled = imageSize * _zoom;
        var imageMin = center + _pan - scaled * 0.5f;

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(canvasMin, canvasMax, true);

        drawList.AddRectFilled(canvasMin, canvasMax, ImGui.GetColorU32(ImGuiCol.FrameBg));
        DrawGrid(drawList, imageMin, canvasMin, canvasMax);

        var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(_red ? 1.0f : 0.0f, _green ? 1.0f : 0.0f, _blue ? 1.0f : 0.0f, 1.0f));

        using (ImGuiDrawCallbacks.Instance.EncodeSrgb(drawList, texture.IsSrgb))
        using (ImGuiDrawCallbacks.Instance.IgnoreAlpha(drawList, !_alpha))
        {
            drawList.AddImage(texture.GetPointer(), imageMin, imageMin + scaled, Vector2.Zero, Vector2.One, tint);
        }

        drawList.AddRect(imageMin, imageMin + scaled, ImGui.GetColorU32(ImGuiCol.Border));

        drawList.PopClipRect();

        if (hovered) TrackHoveredTexel(texture, imageMin, scaled);
    }

    /// <summary>
    /// Texel-aligned grid anchored to the image origin, so it pans and zooms with the image. The step
    /// doubles as it shrinks, keeping the on-screen spacing readable at any zoom.
    /// </summary>
    private void DrawGrid(ImDrawListPtr drawList, Vector2 origin, Vector2 min, Vector2 max)
    {
        var step = 1.0f;
        while (step * _zoom < MinGridSpacing) step *= 2.0f;

        var spacing = step * _zoom;
        var minor = ImGui.GetColorU32(ImGuiCol.Border, 0.35f);
        var major = ImGui.GetColorU32(ImGuiCol.Border, 0.8f);

        for (var column = (int) MathF.Ceiling((min.X - origin.X) / spacing); origin.X + column * spacing <= max.X; column++)
        {
            var x = origin.X + column * spacing;
            drawList.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), column % MajorGridEvery == 0 ? major : minor);
        }

        for (var row = (int) MathF.Ceiling((min.Y - origin.Y) / spacing); origin.Y + row * spacing <= max.Y; row++)
        {
            var y = origin.Y + row * spacing;
            drawList.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), row % MajorGridEvery == 0 ? major : minor);
        }
    }

    private void TrackHoveredTexel(Texture texture, Vector2 imageMin, Vector2 scaled)
    {
        if (scaled.X <= 0.0f || scaled.Y <= 0.0f) return;

        var local = (ImGui.GetIO().MousePos - imageMin) / scaled;
        if (local.X is < 0.0f or > 1.0f || local.Y is < 0.0f or > 1.0f) return;

        _hoveredTexel = new Vector2((int) (local.X * texture.Width), (int) (local.Y * texture.Height));
    }
}
