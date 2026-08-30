using System.Collections.Concurrent;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using Editor.Managers;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Snooper;
using Snooper.Core;
using Snooper.Core.Hardware;

namespace Editor;

public partial class EditorWindow : GameWindow
{
    public readonly ImGuiManager Manager;
    public readonly bool CloseHides;

    public EditorWindow(double fps, int width, int height, IFileProvider fileProvider, bool startVisible = true, bool closeHides = false) : base(
        new GameWindowSettings { UpdateFrequency = fps },
        new NativeWindowSettings
        {
            ClientSize = new OpenTK.Mathematics.Vector2i(width, height),
            WindowBorder = WindowBorder.Resizable,
#if DEBUG
            Flags = ContextFlags.ForwardCompatible | ContextFlags.Debug,
#else
            Flags = ContextFlags.ForwardCompatible,
#endif
            Profile = ContextProfile.Core,
            Vsync = VSyncMode.Off,
            APIVersion = new Version(4, 6),
            AutoIconify = false,
            StartVisible = startVisible,
            StartFocused = startVisible,
            Title = $"Snooper ({Settings.APP_SHORT_COMMIT_ID} - {Settings.APP_BUILD_DATE:MMM d, yyyy})"
        })
    {
        PropertyUtil.SearchPropertyInTemplate = true; // search template properties when looking for a prop via GetOrDefault and cie
        // if (Flags.HasFlag(ContextFlags.Debug))
        // {
        //     Profiler.Enabled = true;
        //     RendererInfo.TrackMemory = true;
        // }

        CloseHides = closeHides;
        Manager = new EditorManager(this, fileProvider);

        Load += DoLoad; // right before the game loop starts
        UpdateFrame += DoUpdate;
        RenderFrame += DoRender;
        TextInput += DoTextInput;
        FramebufferResize += DoResize;
        Closing += args => // this actually runs inside the game loop, it's triggered by SetWindowShouldClose
        {
            if (!CloseHides || _exiting) return;

            // if we are here we are requesting the close button to simply unload the scene and hide the window
            args.Cancel = true;
            Manager.UnloadScene();
            Hide();
        };
    }

    private void DoLoad()
    {
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.PatchParameter(PatchParameterInt.PatchVertices, 4);

#if DEBUG
        GL.DebugMessageCallback(_debugMessageDelegate, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
#endif

        Manager.Load();

        CenterWindow();
        IsVisible = true;

        OnFramebufferResize(new FramebufferResizeEventArgs(ClientSize)); // we initialize a bunch of stuff to 1x1 by default until we know the true size of the framebuffer
    }

    private readonly ConcurrentQueue<Action> _commands = new();
    public void Invoke(Action command)
    {
        _commands.Enqueue(command);
        GLFW.PostEmptyEvent();
    }

    private bool _exiting;
    public void Shutdown() => Invoke(() =>
    {
        _exiting = true;
        Close();
    });

    public void Show()
    {
        IsEventDriven = false;
        IsVisible = true;

        GLFW.PostEmptyEvent();
    }

    public void Hide()
    {
        IsVisible = false;
        IsEventDriven = true;
    }

    private void DoUpdate(FrameEventArgs args)
    {
        while (_commands.TryDequeue(out var command))
        {
            command();
        }

        using (Profiler.Cpu("Update"))
        {
            Manager.Update((float) args.Time);
        }
    }

    private void DoRender(FrameEventArgs args)
    {
        if (!IsVisible) return;

        Profiler.BeginFrame();
        try
        {
            Manager.Render();
            WaitForGpu();
            SwapBuffers();
        }
        finally
        {
            Profiler.EndFrame();
        }
    }

    private const int MaxFramesInFlight = 2;
    private readonly Queue<IntPtr> _frameFences = new();
    private void WaitForGpu()
    {
        using var _ = Profiler.Cpu("Wait for GPU");

        _frameFences.Enqueue(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None));
        if (_frameFences.Count <= MaxFramesInFlight) return;

        var fence = _frameFences.Dequeue();
        GL.ClientWaitSync(fence, ClientWaitSyncFlags.SyncFlushCommandsBit, long.MaxValue);
        GL.DeleteSync(fence);
    }

    private void DoTextInput(TextInputEventArgs e)
    {
        if (!IsFocused) return;

        Manager.TextInput((char) e.Unicode);
    }

    private void DoResize(FramebufferResizeEventArgs e)
    {
        if (!IsFocused) return;

        Log.Information("Framebuffer resized to {Width}x{Height}", e.Width, e.Height);
        Manager.Resize(e.Width, e.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CursorState = CursorState.Normal;
            Manager.Dispose();

            while (_frameFences.TryDequeue(out var fence))
            {
                GL.DeleteSync(fence);
            }
        }

        base.Dispose(disposing);
    }
}
