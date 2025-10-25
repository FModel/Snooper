using System.Numerics;
using ImGuiNET;

namespace Snooper.UI;

public class TexturePreviewWindow
{
    private static readonly Dictionary<string, TexturePreviewWindow> Windows = new();
    
    private readonly string _id;
    private readonly string _title;
    private readonly IntPtr _texturePtr;
    private readonly Vector2 _textureSize;
    
    private bool _isOpen = true;
    private float _zoom = 1.0f;
    private Vector2 _pan = Vector2.Zero;
    private Vector2 _lastMousePos = Vector2.Zero;
    private bool _isDragging;
    private bool _firstFrame = true;
    
    private TexturePreviewWindow(string id, string title, IntPtr texturePtr, Vector2 textureSize)
    {
        _id = id;
        _title = title;
        _texturePtr = texturePtr;
        _textureSize = textureSize;
    }
    
    public static void Open(string id, string title, IntPtr texturePtr, Vector2 textureSize)
    {
        if (Windows.TryGetValue(id, out var w))
        {
            w._isOpen = true;
            return;
        }
        
        Windows[id] = new TexturePreviewWindow(id, title, texturePtr, textureSize);
    }
    
    public static void DrawAll()
    {
        var toRemove = new List<string>();
        
        foreach (var (id, window) in Windows)
        {
            if (!window._isOpen)
            {
                toRemove.Add(id);
                continue;
            }
            
            window.Draw();
        }
        
        foreach (var id in toRemove)
        {
            Windows.Remove(id);
        }
    }
    
    private void Draw()
    {
        if (_texturePtr == IntPtr.Zero)
        {
            _isOpen = false;
            return;
        }
        
        var viewportSize = ImGui.GetIO().DisplaySize;
        var targetSize = viewportSize.X * 0.35f; // 35% of screen width
        var aspectRatio = _textureSize.X / _textureSize.Y;
            
        var windowSize = new Vector2(targetSize * aspectRatio, targetSize);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Appearing);
        
        if (!ImGui.Begin($"{_title}###{_id}", ref _isOpen, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            return;
        }
        
        // Controls
        ImGui.Text($"Texture Size: {_textureSize.X}x{_textureSize.Y}");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 200);
        ImGui.Text($"Zoom: {_zoom * 100:F0}%%");
        
        ImGui.Separator();
        
        // Get available space for texture display
        var availableSize = ImGui.GetContentRegionAvail();
        var drawPos = ImGui.GetCursorScreenPos();
        
        // Initialize zoom and pan to fit and center the texture on first frame
        if (_firstFrame)
        {
            _firstFrame = false;
            
            // Calculate zoom to fit texture in available space
            var scaleX = availableSize.X / _textureSize.X;
            var scaleY = availableSize.Y / _textureSize.Y;
            _zoom = Math.Min(scaleX, scaleY);
            _zoom = Math.Clamp(_zoom, 0.1f, 10.0f);
            
            // Center the texture
            _pan = (availableSize - (_textureSize * _zoom)) * 0.5f;
        }
        
        // Handle mouse wheel zoom
        if (ImGui.IsWindowHovered() && ImGui.IsWindowFocused())
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0)
            {
                var mousePos = ImGui.GetMousePos();
                var relativePos = new Vector2(mousePos.X - drawPos.X, mousePos.Y - drawPos.Y);
                
                // Zoom towards mouse position
                var oldZoom = _zoom;
                _zoom *= 1.0f + wheel * 0.1f;
                _zoom = Math.Clamp(_zoom, 0.1f, 10.0f);
                
                // Adjust pan to zoom towards mouse
                var zoomFactor = _zoom / oldZoom;
                _pan = (_pan - relativePos) * zoomFactor + relativePos;
            }
        }
        
        // Handle panning with middle mouse or right mouse button
        if (ImGui.IsWindowHovered())
        {
            var mousePos = ImGui.GetMousePos();
            
            if ((ImGui.IsMouseDown(ImGuiMouseButton.Middle) || ImGui.IsMouseDown(ImGuiMouseButton.Right)))
            {
                if (!_isDragging)
                {
                    _isDragging = true;
                    _lastMousePos = mousePos;
                }
                else
                {
                    var delta = new Vector2(mousePos.X - _lastMousePos.X, mousePos.Y - _lastMousePos.Y);
                    _pan += delta;
                    _lastMousePos = mousePos;
                }
            }
            else
            {
                _isDragging = false;
            }
        }
        
        // Reset button
        ImGui.SetCursorScreenPos(new Vector2(drawPos.X + availableSize.X - 80, drawPos.Y + 5));
        if (ImGui.Button("Reset View", new Vector2(75, 0)))
        {
            _firstFrame = true;
        }
        
        // Draw texture with zoom and pan
        var drawList = ImGui.GetWindowDrawList();
        
        // Create a child region for clipping
        ImGui.SetCursorScreenPos(drawPos);
        ImGui.InvisibleButton("##canvas", availableSize);
        
        // Calculate texture display size
        var displaySize = _textureSize * _zoom;
        var textureTopLeft = drawPos + _pan;
        var textureBottomRight = textureTopLeft + displaySize;
        
        // Draw checkerboard background
        DrawCheckerboard(drawList, drawPos, availableSize);
        
        // Clip the texture to the available area
        drawList.PushClipRect(drawPos, drawPos + availableSize, true);
        drawList.AddImage(_texturePtr, textureTopLeft, textureBottomRight);
        drawList.AddRect(textureTopLeft, textureBottomRight, ImGui.GetColorU32(new Vector4(1.0f)));
        drawList.PopClipRect();
        
        // Draw border
        drawList.AddRect(drawPos, drawPos + availableSize, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1.0f)));
        
        // Instructions at the bottom
        ImGui.SetCursorScreenPos(new Vector2(drawPos.X + 5, drawPos.Y + availableSize.Y - 40));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.6f);
        ImGui.TextUnformatted("Mouse Wheel: Zoom");
        ImGui.TextUnformatted("Right/Middle Mouse: Pan");
        ImGui.PopStyleVar();
        
        ImGui.End();
    }
    
    private void DrawCheckerboard(ImDrawListPtr drawList, Vector2 topLeft, Vector2 size)
    {
        const float checkerSize = 16.0f;
        var color1 = ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        var color2 = ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.25f, 1.0f));
        
        var cols = (int)Math.Ceiling(size.X / checkerSize);
        var rows = (int)Math.Ceiling(size.Y / checkerSize);
        
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var color = ((row + col) % 2 == 0) ? color1 : color2;
                var min = new Vector2(topLeft.X + col * checkerSize, topLeft.Y + row * checkerSize);
                var max = new Vector2(
                    Math.Min(min.X + checkerSize, topLeft.X + size.X),
                    Math.Min(min.Y + checkerSize, topLeft.Y + size.Y)
                );
                drawList.AddRectFilled(min, max, color);
            }
        }
    }
}
