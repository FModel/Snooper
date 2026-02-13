using System.Numerics;
using System.Reflection;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Managers;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.UI.Systems;

public abstract class InterfaceSystem : SceneManager
{
    protected bool Enabled { get; private set; } = true;
    protected Dictionary<string, Texture> Icons { get; } = new();
    protected NotificationManager Notifications { get; } = new();

    private Actor? _selectedActor;
    protected Actor? SelectedActor
    {
        get => _selectedActor;
        set
        {
            if (_selectedActor == value)
                return;

            ClearSelection();

            _selectedActor = value;
            _selectedComponent = null;
            if (_selectedActor is not null)
            {
                Log.Debug("Selected Actor: {ActorName}", _selectedActor.Name);
                _selectedActor.IsSelected = true;
            }
        }
    }

    private ActorComponent? _selectedComponent;
    protected ActorComponent? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (_selectedComponent == value)
                return;

            ClearSelection();

            _selectedComponent = value;
            _selectedActor = _selectedComponent?.Actor;
            if (_selectedComponent is not null)
            {
                Log.Debug("Selected Component ID: {ComponentId}", _selectedComponent.Id);
                _selectedComponent.IsSelected = true;

                // mark actor as selected but don't outline all its components
                if (_selectedComponent.Actor is not null)
                    _selectedComponent.Actor._isSelected = true;
            }
        }
    }

    private readonly ImGuiController _controller;

    protected InterfaceSystem(GameWindow wnd) : base(wnd)
    {
        _controller = new ImGuiController(Window.ClientSize.X, Window.ClientSize.Y);
    }

    private void ClearSelection()
    {
        _selectedActor?.IsSelected = false;

        if (_selectedComponent is not null)
        {
            _selectedComponent.IsSelected = false;
            _selectedComponent.Actor?._isSelected = false;
        }
    }

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

    public sealed override void Update(float delta)
    {
        var pressed = Window.IsKeyPressed(Keys.F10);
        if (pressed) Enabled = !Enabled;

        if (Enabled)
        {
            _controller.Update(Window, delta);
        }
        else
        {
            if (Window.IsMouseButtonPressed(MouseButton.Right))
                Window.CursorState = CursorState.Grabbed;

            // if (MainCamera is not null)
            // {
            //     if (wnd.IsMouseButtonPressed(MouseButton.Left) && ActiveCamera.PairIndex < Pairs.Count)
            //     {
            //         var mousePos = new Vector2(wnd.MousePosition.X, wnd.MousePosition.Y);
            //         var viewportSize = new Vector2(wnd.ClientSize.X, wnd.ClientSize.Y);
            //         var componentId = Pairs[ActiveCamera.PairIndex].ReadPickingPixel(mousePos, Vector2.Zero, viewportSize);
            //         SelectedComponent = FindComponentById(componentId);
            //     }
            // }
        }

        if (!ImGui.GetIO().WantTextInput) MainViewport?.Camera.Update(Window.KeyboardState, delta);
        if (Window.CursorState == CursorState.Grabbed)
        {
            if (MainViewport != null && Window.MouseState.ScrollDelta.Y != 0)
            {
                var multiplier = Window.KeyboardState.IsKeyDown(Keys.LeftShift) ? 5 : 1f;
                MainViewport.Camera.MovementSpeed += Window.MouseState.ScrollDelta.Y * multiplier;
                MainViewport.Camera.MovementSpeed = MathF.Max(1f, MainViewport.Camera.MovementSpeed);
                Notifications.PushNotification("Camera", () => $"Movement speed set to {MainViewport.Camera.MovementSpeed}.");
            }

            MainViewport?.Camera.Update(Window.MouseState.Delta.X, Window.MouseState.Delta.Y);
            if (Window.IsMouseButtonReleased(MouseButton.Right)) Window.CursorState = CursorState.Normal;
        }

        base.Update(delta);
    }

    public sealed override void Render()
    {
        base.Render();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        if (Enabled)
        {
            RenderInterface();
            _controller.Render();
        }
        else
        {
            Pipeline.RenderToScreen(Window.ClientSize.X, Window.ClientSize.Y);
        }
    }

    protected abstract void RenderInterface();

    protected void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize)
    {
        SelectedComponent = GetComponentById(GetComponentId(mousePos, windowPos, windowSize));
    }

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        _controller.Resize(newWidth, newHeight);
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

    public override long Allocated => base.Allocated + Icons.Values.Sum(i => i.Allocated);
    public override long Used => base.Used + Icons.Values.Sum(i => i.Used);
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;

        // foreach (var icon in Icons.Values)
        // {
        //     yield return new MemoryDetail(icon.Name, icon);
        // }
    }
}
