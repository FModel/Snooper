using System.Numerics;
using System.Reflection;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.UI.Systems;

public abstract class InterfaceSystem(GameWindow wnd) : SceneSystem(wnd)
{
    private readonly ImGuiController _controller = new(wnd.ClientSize.X, wnd.ClientSize.Y);
    
    private WindowState _pWindowState;

    protected bool Enabled { get; private set; } = true;
    protected Dictionary<string, Texture> Icons { get; } = new();
    protected NotificationManager Notifications { get; } = new();
    
    private uint _selectedComponentId;
    protected uint SelectedComponentId
    {
        get => _selectedComponentId;
        set
        {
            if (_selectedComponentId == value)
                return;
            
            if (SelectedComponent?.Actor != null)
                foreach (var c in SelectedComponent.Actor.Components)
                    c.IsSelected = false;
            
            _selectedComponentId = value;
            Log.Debug("Selected Component ID: {ComponentId}", _selectedComponentId);
            
            SelectedComponent = FindComponentById(_selectedComponentId);
            if (SelectedComponent is not null)
                SelectedComponent.IsSelected = true;
            
            foreach (var pair in Pairs)
            {
                pair.OverridePickingId(_selectedComponentId); // enables outline rendering
            }
        }
    }
    
    protected ActorComponent? SelectedComponent { get; private set; }
    
    public override void Load()
    {
        _controller.Load();
        Theme();
        
        var icons = Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(x => x.StartsWith("Snooper.UI.Textures.") && x.EndsWith(".png"))
            .Select(x => x["Snooper.".Length..]).ToList();
        foreach (var icon in icons)
        {
            var texture = new EmbeddedTexture2D(icon);
            texture.Generate();
                
            Icons.Add(icon["UI.Textures.".Length..^4], texture);
        }
        
        base.Load();
    }

    public override void Update(float delta)
    {
        var pressed = Window.IsKeyPressed(Keys.F10);
        if (pressed) Enabled = !Enabled;

        if (Window.IsKeyPressed(Keys.F))
        {
            if (Window.WindowState == WindowState.Fullscreen)
            {
                Window.WindowState = _pWindowState;
            }
            else
            {
                _pWindowState = Window.WindowState;
                Window.WindowState = WindowState.Fullscreen;
            }
        }
        
        if (Enabled)
            _controller.Update(Window, delta);
        else if (Window.IsMouseButtonPressed(MouseButton.Right))
            Window.CursorState = CursorState.Grabbed;
        
        if (ActiveCamera is null && Pairs.Count > 0)
            ActiveCamera = Pairs[0].Camera;

        if (ActiveCamera is not null)
        {
            if (pressed && !Enabled)
                ActiveCamera.ViewportSize = new Vector2(Window.ClientSize.X, Window.ClientSize.Y);
        }
        
        ActiveCamera?.Update(Window.KeyboardState, delta);
        if (Window.CursorState == CursorState.Grabbed)
        {
            ActiveCamera?.Update(Window.MouseState.Delta.X, Window.MouseState.Delta.Y);
            if (Window.IsMouseButtonReleased(MouseButton.Right)) Window.CursorState = CursorState.Normal;
        }
        
        base.Update(delta);
    }

    public sealed override void Render()
    {
        base.Render();
        
        if (Enabled)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.ClearColor(0, 0, 0, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            
            RenderInterface();
            _controller.Render();
        }
        else if (ActiveCamera is not null && ActiveCamera.PairIndex < Pairs.Count)
        {
            Pairs[ActiveCamera.PairIndex].RenderToScreen(Window.ClientSize.X, Window.ClientSize.Y);
        }
    }
    
    protected abstract void RenderInterface();

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);
        
        _controller.Resize(newWidth, newHeight);
    }
    
    private ActorComponent? FindComponentById(uint componentId)
    {
        if (componentId == 0 || RootActor == null)
            return null;
        
        return FindRecursive(RootActor);

        ActorComponent? FindRecursive(Actor actor)
        {
            foreach (var component in actor.Components)
            {
                if (component.Id == componentId)
                {
                    return component;
                }
            }
            
            foreach (var child in actor.Children)
            {
                var found = FindRecursive(child);
                if (found != null)
                    return found;
            }
            
            return null;
        }
    }

    public void TextInput(char c) => _controller.TextInput(c);
    
    private void Theme()
    {
        var style = ImGui.GetStyle();
        style.WindowPadding = new Vector2(4f);
        style.FramePadding = new Vector2(3f);
        style.CellPadding = new Vector2(3f, 2f);
        style.ItemSpacing = new Vector2(6f, 3f);
        style.ItemInnerSpacing = new Vector2(3f);
        style.TouchExtraPadding = new Vector2(0f);
        style.IndentSpacing = 20f;
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 8f;
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 0f;
        style.FrameBorderSize = 0f;
        style.TabBorderSize = 0f;
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 0f;
        style.PopupRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.GrabRounding = 0f;
        style.LogSliderDeadzone = 0f;
        style.TabRounding = 0f;
        style.WindowTitleAlign = new Vector2(0.5f);
        style.WindowMenuButtonPosition = ImGuiDir.Right;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f);
        style.SelectableTextAlign = new Vector2(0f);
        style.DisplaySafeAreaPadding = new Vector2(3f);
        

        style.Colors[(int) ImGuiCol.Text]                   = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
        style.Colors[(int) ImGuiCol.TextDisabled]           = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        style.Colors[(int) ImGuiCol.WindowBg]               = new Vector4(0.11f, 0.11f, 0.12f, 1.00f);
        style.Colors[(int) ImGuiCol.ChildBg]                = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        style.Colors[(int) ImGuiCol.PopupBg]                = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
        style.Colors[(int) ImGuiCol.Border]                 = new Vector4(0.25f, 0.26f, 0.33f, 1.00f);
        style.Colors[(int) ImGuiCol.BorderShadow]           = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style.Colors[(int) ImGuiCol.FrameBg]                = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style.Colors[(int) ImGuiCol.FrameBgHovered]         = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.FrameBgActive]          = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        style.Colors[(int) ImGuiCol.TitleBg]                = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TitleBgActive]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TitleBgCollapsed]       = new Vector4(0.05f, 0.05f, 0.05f, 0.51f);
        style.Colors[(int) ImGuiCol.MenuBarBg]              = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarBg]            = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
        style.Colors[(int) ImGuiCol.ScrollbarGrab]          = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarGrabHovered]   = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
        style.Colors[(int) ImGuiCol.ScrollbarGrabActive]    = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
        style.Colors[(int) ImGuiCol.CheckMark]              = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        style.Colors[(int) ImGuiCol.SliderGrab]             = new Vector4(0.13f, 0.42f, 0.83f, 0.78f);
        style.Colors[(int) ImGuiCol.SliderGrabActive]       = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        style.Colors[(int) ImGuiCol.Button]                 = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        style.Colors[(int) ImGuiCol.ButtonHovered]          = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.ButtonActive]           = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        style.Colors[(int) ImGuiCol.Header]                 = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int) ImGuiCol.HeaderHovered]          = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.HeaderActive]           = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.Separator]              = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
        style.Colors[(int) ImGuiCol.SeparatorHovered]       = new Vector4(0.10f, 0.40f, 0.75f, 0.78f);
        style.Colors[(int) ImGuiCol.SeparatorActive]        = new Vector4(0.10f, 0.40f, 0.75f, 1.00f);
        style.Colors[(int) ImGuiCol.ResizeGrip]             = new Vector4(0.13f, 0.42f, 0.83f, 0.39f);
        style.Colors[(int) ImGuiCol.ResizeGripHovered]      = new Vector4(0.12f, 0.41f, 0.81f, 0.78f);
        style.Colors[(int) ImGuiCol.ResizeGripActive]       = new Vector4(0.12f, 0.41f, 0.81f, 1.00f);
        style.Colors[(int) ImGuiCol.Tab]                    = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        style.Colors[(int) ImGuiCol.TabHovered]             = new Vector4(0.35f, 0.35f, 0.41f, 0.80f);
        style.Colors[(int) ImGuiCol.TabSelected]            = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        style.Colors[(int) ImGuiCol.TabDimmed]              = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        style.Colors[(int) ImGuiCol.TabDimmedSelected]      = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        style.Colors[(int) ImGuiCol.DockingPreview]         = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
        style.Colors[(int) ImGuiCol.DockingEmptyBg]         = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotLines]              = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotLinesHovered]       = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotHistogram]          = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
        style.Colors[(int) ImGuiCol.PlotHistogramHovered]   = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
        style.Colors[(int) ImGuiCol.TableHeaderBg]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        style.Colors[(int) ImGuiCol.TableBorderStrong]      = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.TableBorderLight]       = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        style.Colors[(int) ImGuiCol.TableRowBg]             = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        style.Colors[(int) ImGuiCol.TableRowBgAlt]          = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
        style.Colors[(int) ImGuiCol.TextSelectedBg]         = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
        style.Colors[(int) ImGuiCol.DragDropTarget]         = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
        style.Colors[(int) ImGuiCol.NavCursor]              = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
        style.Colors[(int) ImGuiCol.NavWindowingHighlight]  = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
        style.Colors[(int) ImGuiCol.NavWindowingDimBg]      = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
        style.Colors[(int) ImGuiCol.ModalWindowDimBg]       = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
    }
}