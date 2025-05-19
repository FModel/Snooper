using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Core.Systems;
using Snooper.Rendering;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;
using Snooper.UI;
using Plane = Snooper.Rendering.Primitives.Plane;

namespace Snooper;

public partial class MainWindow : GameWindow
{
    private readonly ImGuiSystem _imgui;
    private readonly SceneSystem _scene;

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        _imgui = new ImGuiSystem();
        _imgui.Resize(ClientSize.X, ClientSize.Y);

        _scene = new SceneSystem();

        var root = new Actor();
        var camera = new Actor();
        camera.Transform.Position += Vector3.UnitZ * 2;
        camera.Components.Add(new CameraComponent());

        var triangle = new Actor();
        triangle.Transform.Position += Vector3.UnitX;
        triangle.Components.Add(new PrimitiveComponent(new Triangle()));

        var plane = new Actor();
        plane.Transform.Position -= Vector3.UnitX;
        plane.Components.Add(new PrimitiveComponent(new Plane()));

        root.Children.Add(camera);
        root.Children.Add(triangle);
        root.Children.Add(plane);
        _scene.RootActor = root;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(OpenTK.Mathematics.Color4.Black);
        // GL.Enable(EnableCap.Blend);
        // GL.Enable(EnableCap.CullFace);
        // GL.Enable(EnableCap.DepthTest);
        // GL.Enable(EnableCap.Multisample);
        // GL.Enable(EnableCap.VertexProgramPointSize);
        // GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);
        // GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
#if DEBUG
        GL.DebugMessageCallback(_debugMessageDelegate, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
#endif

        _scene.Load();
        _imgui.Generate();

        CenterWindow();
        IsVisible = true;
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit);

        _scene.Render();

        // ImGui.ShowDemoWindow();
        _imgui.Render();

        SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        var delta = (float) args.Time;

        _scene.Update(delta);
        _scene.CurrentCamera?.Modify(KeyboardState, delta);

        _imgui.Update(this, delta);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        _imgui.TextInput((char) e.Unicode);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        _imgui.Resize(e.Width, e.Height);
    }
}
