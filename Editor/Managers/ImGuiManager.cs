using System.Diagnostics;
using System.Numerics;
using CUE4Parse.FileProvider;
using ImGuiNET;
using ImGuizmoNET;
using ImPlotNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper;
using Snooper.Core;
using Snooper.Core.Managers;

namespace Editor.Managers;

public abstract class ImGuiManager : SceneManager
{
    private bool _show = true;
    private readonly ImGuiController _controller;

    protected ImGuiManager(GameWindow wnd, IFileProvider fileProvider) : base(wnd, fileProvider)
    {
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        ImGuizmo.SetImGuiContext(context);
        ImPlot.SetImGuiContext(context);
        imnodesNET.imnodes.SetImGuiContext(context);

        ImGuizmo.AllowAxisFlip(false);
        imnodesNET.imnodes.SetCurrentContext(imnodesNET.imnodes.CreateContext());
        ImPlot.SetCurrentContext(ImPlot.CreateContext());

        _controller = new ImGuiController();

        wnd.TryGetCurrentMonitorScale(out var xScale, out var yScale);
        var scale = Math.Min(xScale, yScale);

        var io = ImGui.GetIO();

        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName().Name;

        byte[] LoadFont(string name)
        {
            using var stream = assembly.GetManifestResourceStream($"{assemblyName}.Fonts.{name}")
                ?? throw new FileNotFoundException($"Embedded font '{name}' not found.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        var faSolid   = LoadFont("fa-solid-900.otf");
        var faRegular = LoadFont("fa-regular-400.otf");
        var faBrands  = LoadFont("fa-brands-400.otf");

        unsafe
        {
            // FA5 icons live in E000–F8FF (brands/regular/solid)
            var iconRanges = stackalloc ushort[] { 0xe000, 0xf8ff, 0 };

            var cfg = ImGuiNative.ImFontConfig_ImFontConfig();
            cfg->MergeMode = 1;
            cfg->PixelSnapH = 1;
            cfg->GlyphMinAdvanceX = 16f;
            // FontDataOwnedByAtlas = 0 because we manage the GCHandle lifetime ourselves
            cfg->FontDataOwnedByAtlas = 0;

            void MergeFontAwesome(byte[] solid, byte[] regular, byte[] brands)
            {
                fixed (byte* pSolid   = solid)
                fixed (byte* pRegular = regular)
                fixed (byte* pBrands  = brands)
                {
                    io.Fonts.AddFontFromMemoryTTF((IntPtr)pSolid,   solid.Length,   14, (IntPtr)cfg, (IntPtr)iconRanges);
                    io.Fonts.AddFontFromMemoryTTF((IntPtr)pRegular, regular.Length, 14, (IntPtr)cfg, (IntPtr)iconRanges);
                    io.Fonts.AddFontFromMemoryTTF((IntPtr)pBrands,  brands.Length,  14, (IntPtr)cfg, (IntPtr)iconRanges);
                }
            }

            io.Fonts.AddFontFromFileTTF(@"C:\Windows\Fonts\segoeui.ttf",  16);
            MergeFontAwesome(faSolid, faRegular, faBrands);

            io.Fonts.AddFontFromFileTTF(@"C:\Windows\Fonts\segoeuib.ttf", 16);
            MergeFontAwesome(faSolid, faRegular, faBrands);

            io.Fonts.AddFontFromFileTTF(@"C:\Windows\Fonts\seguisb.ttf",  16);
            MergeFontAwesome(faSolid, faRegular, faBrands);

            ImGuiNative.ImFontConfig_destroy(cfg);
        }

        io.Fonts.AddFontDefault();
        io.Fonts.Build();
        io.FontGlobalScale = scale;

        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigDockingAlwaysTabBar = true;
        io.ConfigDockingWithShift = true;
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        io.ConfigDragClickToInputText = true;
        io.ConfigDebugIsDebuggerPresent = Debugger.IsAttached;
        io.ConfigErrorRecoveryEnableDebugLog = true;
        io.ConfigErrorRecovery = true;
        io.ConfigErrorRecoveryEnableAssert = false;

        var style = ImGui.GetStyle();
        style.ScaleAllSizes(scale);

        style.WindowPadding = new Vector2(4f);
        style.FramePadding = new Vector2(4f);
        style.CellPadding = new Vector2(3f, 2f);
        style.ItemSpacing = new Vector2(6f, 3f);
        style.ItemInnerSpacing = new Vector2(3f);
        style.TouchExtraPadding = new Vector2(0f);
        style.IndentSpacing = 20f;
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 8f;
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 1f;
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
        style.WindowMenuButtonPosition = ImGuiDir.Left;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f);
        style.SelectableTextAlign = new Vector2(0f);
        style.DisplaySafeAreaPadding = new Vector2(3f);

        var colors = style.Colors;
        colors[(int) ImGuiCol.Text]                   = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[(int) ImGuiCol.TextDisabled]           = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        colors[(int) ImGuiCol.WindowBg]               = new Vector4(0.11f, 0.11f, 0.12f, 1.00f);
        colors[(int) ImGuiCol.ChildBg]                = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        colors[(int) ImGuiCol.PopupBg]                = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
        colors[(int) ImGuiCol.Border]                 = new Vector4(0.25f, 0.26f, 0.33f, 1.00f);
        colors[(int) ImGuiCol.BorderShadow]           = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int) ImGuiCol.FrameBg]                = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        colors[(int) ImGuiCol.FrameBgHovered]         = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.FrameBgActive]          = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        colors[(int) ImGuiCol.TitleBg]                = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        colors[(int) ImGuiCol.TitleBgActive]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        colors[(int) ImGuiCol.TitleBgCollapsed]       = new Vector4(0.05f, 0.05f, 0.05f, 0.51f);
        colors[(int) ImGuiCol.MenuBarBg]              = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        colors[(int) ImGuiCol.ScrollbarBg]            = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
        colors[(int) ImGuiCol.ScrollbarGrab]          = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int) ImGuiCol.ScrollbarGrabHovered]   = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
        colors[(int) ImGuiCol.ScrollbarGrabActive]    = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
        colors[(int) ImGuiCol.CheckMark]              = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        colors[(int) ImGuiCol.SliderGrab]             = new Vector4(0.13f, 0.42f, 0.83f, 0.78f);
        colors[(int) ImGuiCol.SliderGrabActive]       = new Vector4(0.13f, 0.42f, 0.83f, 1.00f);
        colors[(int) ImGuiCol.Button]                 = new Vector4(0.05f, 0.05f, 0.05f, 0.54f);
        colors[(int) ImGuiCol.ButtonHovered]          = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.ButtonActive]           = new Vector4(0.69f, 0.69f, 1.00f, 0.39f);
        colors[(int) ImGuiCol.Header]                 = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[(int) ImGuiCol.HeaderHovered]          = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.HeaderActive]           = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.Separator]              = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
        colors[(int) ImGuiCol.SeparatorHovered]       = new Vector4(0.10f, 0.40f, 0.75f, 0.78f);
        colors[(int) ImGuiCol.SeparatorActive]        = new Vector4(0.10f, 0.40f, 0.75f, 1.00f);
        colors[(int) ImGuiCol.ResizeGrip]             = new Vector4(0.13f, 0.42f, 0.83f, 0.39f);
        colors[(int) ImGuiCol.ResizeGripHovered]      = new Vector4(0.12f, 0.41f, 0.81f, 0.78f);
        colors[(int) ImGuiCol.ResizeGripActive]       = new Vector4(0.12f, 0.41f, 0.81f, 1.00f);
        colors[(int) ImGuiCol.Tab]                    = new Vector4(0.15f, 0.15f, 0.19f, 1.00f);
        colors[(int) ImGuiCol.TabHovered]             = new Vector4(0.35f, 0.35f, 0.41f, 0.80f);
        colors[(int) ImGuiCol.TabSelected]            = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        colors[(int) ImGuiCol.TabDimmed]              = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        colors[(int) ImGuiCol.TabDimmedSelected]      = new Vector4(0.23f, 0.24f, 0.29f, 1.00f);
        colors[(int) ImGuiCol.DockingPreview]         = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
        colors[(int) ImGuiCol.DockingEmptyBg]         = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[(int) ImGuiCol.PlotLines]              = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
        colors[(int) ImGuiCol.PlotLinesHovered]       = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
        colors[(int) ImGuiCol.PlotHistogram]          = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[(int) ImGuiCol.PlotHistogramHovered]   = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
        colors[(int) ImGuiCol.TableHeaderBg]          = new Vector4(0.09f, 0.09f, 0.09f, 1.00f);
        colors[(int) ImGuiCol.TableBorderStrong]      = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.TableBorderLight]       = new Vector4(0.69f, 0.69f, 1.00f, 0.20f);
        colors[(int) ImGuiCol.TableRowBg]             = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int) ImGuiCol.TableRowBgAlt]          = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
        colors[(int) ImGuiCol.TextSelectedBg]         = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
        colors[(int) ImGuiCol.DragDropTarget]         = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
        colors[(int) ImGuiCol.NavCursor]              = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[(int) ImGuiCol.NavWindowingHighlight]  = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
        colors[(int) ImGuiCol.NavWindowingDimBg]      = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
        colors[(int) ImGuiCol.ModalWindowDimBg]       = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);
    }

    public override void Load()
    {
        base.Load();

        _controller.Load();
    }

    public override void Update(float delta)
    {
        using (Profiler.Cpu("Inputs"))
        {
            if (Window.IsKeyPressed(Keys.F10))
                _show = !_show;

            if (_show)
            {
                _controller.Update(Window, delta);
            }
            else
            {
                if (Window.IsMouseButtonPressed(MouseButton.Right))
                    Window.CursorState = CursorState.Grabbed;

                if (Window.IsMouseButtonPressed(MouseButton.Left))
                {
                    var mousePos = new Vector2(Window.MousePosition.X, Window.MousePosition.Y);
                    var viewportSize = new Vector2(Window.ClientSize.X, Window.ClientSize.Y);
                    OnViewportLeftClick(mousePos, Vector2.Zero, viewportSize);
                }

                MainViewport?.Camera.Resize(Window.ClientSize.X, Window.ClientSize.Y);
            }

            if (!ImGui.GetIO().WantTextInput)
                MainViewport?.Camera.Update(Window.KeyboardState, delta);

            if (Window.CursorState == CursorState.Grabbed)
            {
                if (MainViewport != null && Window.MouseState.ScrollDelta.Y != 0)
                {
                    var multiplier = Window.KeyboardState.IsKeyDown(Keys.LeftShift) ? 5 : 1f;
                    MainViewport.Camera.MovementSpeed += Window.MouseState.ScrollDelta.Y * multiplier;
                    Notifications.Push("camera.speed", Settings.SpeedIcon, $"Camera Speed {MainViewport.Camera.MovementSpeed:0.#}");
                }

                MainViewport?.Camera.Update(Window.MouseState.Delta.X, Window.MouseState.Delta.Y);
                if (Window.IsMouseButtonReleased(MouseButton.Right)) Window.CursorState = CursorState.Normal;
            }
        }

        base.Update(delta);
    }

    public sealed override void Render()
    {
        base.Render();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        if (!_show)
        {
            Pipeline.RenderToScreen(Window.ClientSize.X, Window.ClientSize.Y);
        }
        else using (Profiler.Sample("ImGui"))
        {
            using (Profiler.Cpu("Widgets")) RenderInterface();
            _controller.Render();
        }
    }

    protected virtual void RenderInterface() => ImGui.DockSpaceOverViewport();
    public abstract void OnViewportLeftClick(Vector2 mousePos, Vector2 windowPos, Vector2 windowSize);

    public void TextInput(char c) => _controller.TextInput(c);

    public override void Resize(int newWidth, int newHeight)
    {
        base.Resize(newWidth, newHeight);

        _controller.Resize(newWidth, newHeight);
    }
}
