using CUE4Parse.UE4.Assets.Exports;
using Editor.Managers;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper;
using Snooper.Rendering.Cache;

namespace Editor;

public partial class EditorWindow : GameWindow
{
    public readonly ImGuiManager Manager;

    public EditorWindow(double fps, int width, int height, bool startVisible = true) : base(
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
            Vsync = VSyncMode.Adaptive,
            APIVersion = new Version(4, 6),
            StartVisible = startVisible,
            StartFocused = startVisible,
            Title = $"Snooper ({Settings.APP_SHORT_COMMIT_ID} - {Settings.APP_BUILD_DATE:MMM d, yyyy})"
        })
    {
        PropertyUtil.SearchPropertyInTemplate = true; // search template properties when looking for a prop via GetOrDefault and cie

        Load += DoLoad; // right before the game loop starts
        UpdateFrame += DoUpdate;
        RenderFrame += DoRender;
        TextInput += DoTextInput;
        FramebufferResize += DoResize;
        Closing += _ => // this actually runs inside the game loop, it's triggered by SetWindowShouldClose
        {
            MeshCache.ClearAndDispose();
            MaterialCache.ClearAndDispose();
        };
        Unload += DoUnload; // right after the game loop ends

        Manager = new EditorManager(this);
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        OnFramebufferResize(new FramebufferResizeEventArgs(ClientSize)); // we initialize a bunch of stuff to 1x1 by default until we know the true size of the framebuffer
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
    }

    private void DoUpdate(FrameEventArgs args)
    {
        if (!IsFocused) return;

        var delta = (float) args.Time;
        Manager.Update(delta);
    }

    private void DoRender(FrameEventArgs args)
    {
        if (!IsFocused) return;

        Manager.Render();

        SwapBuffers();
    }

    private void DoTextInput(TextInputEventArgs e)
    {
        if (!IsFocused) return;

        Manager.TextInput((char) e.Unicode);
    }

    private void DoResize(FramebufferResizeEventArgs e)
    {
        Manager.Resize(e.Width, e.Height);
    }

    private void DoUnload()
    {
        Manager.Dispose();
        Log.CloseAndFlush();
    }
}
