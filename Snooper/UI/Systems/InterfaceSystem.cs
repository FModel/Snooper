using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;

namespace Snooper.UI.Systems;

public abstract class InterfaceSystem(GameWindow wnd) : SceneSystem(wnd)
{
    private readonly ImGuiController _controller = new(wnd.ClientSize.X, wnd.ClientSize.Y);
    
    private WindowState _pWindowState;
    
    protected bool Enabled { get; private set; } = true;
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
        }
    }
    
    protected ActorComponent? SelectedComponent { get; private set; }
    
    public override void Load()
    {
        _controller.Load();
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
        else if (Window.IsMouseButtonPressed(MouseButton.Left))
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
            if (Window.IsMouseButtonReleased(MouseButton.Left)) Window.CursorState = CursorState.Normal;
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
}