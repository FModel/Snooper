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
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper;

public partial class MainWindow : GameWindow
{
    private readonly SceneSystem _sceneSystem;
    private readonly ImGuiSystem _imguiSystem;

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        ActorManager.RegisterSystemFactory<TransformSystem>();
        ActorManager.RegisterSystemFactory<CameraSystem>();
        ActorManager.RegisterSystemFactory<PrimitiveSystem>();
        ActorManager.RegisterSystemFactory<RenderSystem>();

        _sceneSystem = new SceneSystem(this);

        _imguiSystem = new ImGuiSystem();
        _imguiSystem.Resize(ClientSize.X, ClientSize.Y);

        var root = new Actor("Root");

        var camera1 = new Actor("Camera 1");
        camera1.Transform.Position -= Vector3.UnitZ * 2;
        camera1.Components.Add(new CameraComponent());
        root.Children.Add(camera1);

        var camera2 = new Actor("Camera 2");
        camera2.Transform.Position -= Vector3.UnitZ * 2;
        camera2.Transform.Position += Vector3.UnitY * .5f;
        camera2.Components.Add(new CameraComponent());
        camera2.Components.Add(new PrimitiveComponent(new Triangle()));
        root.Children.Add(camera2);

        // var triangle = new Actor("Triangle");
        // triangle.Transform.Position += Vector3.UnitX;
        // triangle.Components.Add(new PrimitiveComponent(new Triangle()));
        // root.Children.Add(triangle);
        
        var plane = new Actor("SM 1");
        plane.Components.Add(new StaticMeshComponent());
        root.Children.Add(plane);

        _sceneSystem.RootActor = root;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        // GL.Enable(EnableCap.Blend);
        // GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        // GL.Enable(EnableCap.Multisample);
        // GL.Enable(EnableCap.VertexProgramPointSize);
        // GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);
        // GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
#if DEBUG
        GL.DebugMessageCallback(_debugMessageDelegate, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
#endif

        _sceneSystem.Load();
        _imguiSystem.Load();

        CenterWindow();
        IsVisible = true;
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        var delta = (float) args.Time;
        _sceneSystem.Update(delta);
        _imguiSystem.Update(this, delta);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        _sceneSystem.Render();

        GL.ClearColor(OpenTK.Mathematics.Color4.Black);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        foreach (var pair in _sceneSystem.Pairs)
        {
            if (ImGui.Begin($"Viewport ({pair.Camera.Actor?.Name})"))
            {
                if (ImGui.IsWindowFocused()) _sceneSystem.CurrentCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var size = new Vector2(largest.X, largest.Y);
                pair.Camera.AspectRatio = size.X / size.Y;
                ImGui.Image(pair.Framebuffer.GetPointer(), size, Vector2.UnitY, Vector2.UnitX);

                const float margin = 7.5f;
                const string label = "Previewed content may differ from final version saved or used in-game.";
                ImGui.SetCursorPos(size with { X = size.X - ImGui.CalcTextSize(label).X - margin });
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.50f), label);
            }
            ImGui.End();
        }
        ImGui.PopStyleVar();

        _imguiSystem.Render();

        SwapBuffers();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        _imguiSystem.TextInput((char) e.Unicode);
    }

    protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
    {
        base.OnFramebufferResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);
        foreach (var pair in _sceneSystem.Pairs)
            pair.Framebuffer.Resize(e.Width, e.Height);
        _imguiSystem.Resize(e.Width, e.Height);
    }
}
