using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core.Systems;

namespace Snooper.UI.Systems;

public abstract class InterfaceSystem(GameWindow wnd) : SceneSystem(wnd)
{
    private readonly ImGuiController _controller = new(wnd.ClientSize.X, wnd.ClientSize.Y);
    
    private const float KeyDownCooldown = 0.1f;
    private float _keyDownTimer;
    
    protected bool Enabled { get; private set; } = true;
    protected NotificationManager Notifications { get; } = new();

    public override void Load()
    {
        _controller.Load();
        base.Load();
    }

    public override void Update(float delta)
    {
        _keyDownTimer -= delta;
        
        var pressed = Window.IsKeyPressed(Keys.F10);
        if (pressed) Enabled = !Enabled;
        
        if (Window.IsKeyPressed(Keys.F))
            Window.WindowState = Window.WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen;
        
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

            if (_keyDownTimer <= 0)
            {
                if (Window.IsKeyDown(Keys.PageUp))
                {
                    ActiveCamera.MovementSpeed += 5f;
                    Notifications.PushNotification("Camera", $"Movement speed increased to {ActiveCamera.MovementSpeed}.");
                    _keyDownTimer = KeyDownCooldown;
                }
                else if (Window.IsKeyDown(Keys.PageDown))
                {
                    ActiveCamera.MovementSpeed = MathF.Max(1, ActiveCamera.MovementSpeed - 5f);
                    Notifications.PushNotification("Camera", $"Movement speed decreased to {ActiveCamera.MovementSpeed}.");
                    _keyDownTimer = KeyDownCooldown;
                }
            }
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

    public void TextInput(char c) => _controller.TextInput(c);
}