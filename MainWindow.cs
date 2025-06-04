using System.Numerics;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Core.Systems;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;
using Plane = Snooper.Rendering.Primitives.Plane;

namespace Snooper;

public partial class MainWindow : GameWindow
{
    private readonly SceneSystem _sceneSystem;
    private readonly ImGuiSystem _imguiSystem;

    public MainWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
    {
        ActorManager.RegisterSystemFactory<GridSystem>();
        ActorManager.RegisterSystemFactory<TransformSystem>();
        ActorManager.RegisterSystemFactory<CameraSystem>();
        ActorManager.RegisterSystemFactory<CullingSystem>();
        ActorManager.RegisterSystemFactory<PrimitiveSystem>();
        ActorManager.RegisterSystemFactory<DeferredRenderSystem>();
        ActorManager.RegisterSystemFactory<RenderSystem>();
        ActorManager.RegisterSystemFactory<DebugSystem>();

        _sceneSystem = new SceneSystem(this);

        _imguiSystem = new ImGuiSystem();
        _imguiSystem.Resize(ClientSize.X, ClientSize.Y);

        var root = new Actor("Scene");

        var grid = new Actor("Grid");
        grid.Components.Add(new GridComponent());
        root.Children.Add(grid);

        var camera1 = new Actor("Camera 1");
        camera1.Transform.Position -= Vector3.UnitZ * 2;
        camera1.Transform.Position += Vector3.UnitY;
        camera1.Transform.Position -= Vector3.UnitX;
        camera1.Components.Add(new CameraComponent());
        root.Children.Add(camera1);

        var camera2 = new CameraActor("Camera 2");
        camera2.Transform.Position -= Vector3.UnitZ * 2;
        camera2.Transform.Position += Vector3.UnitY;
        camera2.Transform.Position += Vector3.UnitX;
        root.Children.Add(camera2);

        var plane = new Actor("Plane");
        plane.Transform.Position += Vector3.UnitY * 2.5f;
        plane.Components.Add(new PrimitiveComponent(new Plane(Vector3.UnitY)));
        plane.Components.Add(new BoxCullingComponent(Vector3.Zero, new Vector3(1, 0, 1)));
        root.Children.Add(plane);

        var sphere = new Actor("Sphere");
        sphere.Transform.Position -= Vector3.UnitX * 2;
        sphere.Components.Add(new PrimitiveComponent(new Sphere(18, 9, 0.5f)));
        sphere.Components.Add(new BoxCullingComponent(Vector3.Zero, new Vector3(0.5f)));
        root.Children.Add(sphere);

        _sceneSystem.RootActor = root;
    }

    public void Insert(UStaticMesh mesh)
    {
        // if (mesh.TryConvert(out var primitiveData))
        {
            var actor = new MeshActor(mesh);
            actor.Transform.Position += Vector3.UnitX;
            _sceneSystem.RootActor?.Children.Add(actor);
        }
    }

    public void Insert(USkeletalMesh mesh)
    {
        // if (mesh.TryConvert(out var primitiveData))
        {
            var actor = new MeshActor(mesh);
            actor.Transform.Position -= Vector3.UnitX;
            _sceneSystem.RootActor?.Children.Add(actor);
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.Enable(EnableCap.Multisample);
        // GL.Enable(EnableCap.VertexProgramPointSize);
        // GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
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

        var query = GL.GenQuery();
        GL.BeginQuery(QueryTarget.PrimitivesGenerated, query);
        _sceneSystem.Render();
        GL.EndQuery(QueryTarget.PrimitivesGenerated);
        GL.GetQueryObject(query, GetQueryObjectParam.QueryResult, out long primitiveCount);

        GL.ClearColor(OpenTK.Mathematics.Color4.Black);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        foreach (var pair in _sceneSystem.Pairs)
        {
            if (ImGui.Begin($"Viewport ({pair.Camera.Actor?.Name})"))
            {
                if (ImGui.IsWindowFocused()) _sceneSystem.ActiveCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var size = new Vector2(largest.X, largest.Y);
                pair.Camera.ViewportSize = size;
                ImGui.Image(pair.Framebuffer.GetPointer(), size, Vector2.UnitY, Vector2.UnitX);

                if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    CursorState = CursorState.Grabbed;
                }
                if (CursorState == CursorState.Grabbed)
                {
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left)) _sceneSystem.ActiveCamera?.Update(ImGui.GetIO().MouseDelta);
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) CursorState = CursorState.Normal;
                }

                const float margin = 7.5f;
                ImGui.SetCursorPos(new Vector2(margin, margin + ImGui.GetFrameHeight()));
                ImGui.Text($"Primitives: {primitiveCount}");

                var framerate = ImGui.GetIO().Framerate;
                ImGui.SetCursorPos(size with { X = margin });
                ImGui.Text($"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)");

                const string label = "Previewed content may differ from final version saved or used in-game.";
                ImGui.SetCursorPos(size with { X = size.X - ImGui.CalcTextSize(label).X - margin });
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 0.50f), label);
            }
            ImGui.End();
        }
        ImGui.PopStyleVar();

        var camera = _sceneSystem.ActiveCamera;
        if (camera != null)
        {
            if (ImGui.Begin("Controls"))
            {
                ImGui.Text($"Current camera: {camera.Actor?.Name}");
                ImGui.Separator();

                ImGui.Text($"Position: {camera.Actor.Transform.Position}");
                ImGui.Text($"Rotation: {camera.Actor.Transform.Rotation}");
                ImGui.Text($"Scale: {camera.Actor.Transform.Scale}");
                ImGui.Text($"ViewportSize: {camera.ViewportSize}");
                ImGui.DragFloat("Near Plane Distance", ref camera.NearPlaneDistance, 0.001f, 0.001f, 0.099f);
                ImGui.DragFloat("Far Plane Distance", ref camera.FarPlaneDistance, 0.1f , camera.NearPlaneDistance, 1000.0f);
            }
            ImGui.End();
        }

        var root = _sceneSystem.RootActor;
        if (root != null)
        {
            if (ImGui.Begin("Scene Hierarchy"))
            {
                ImGui.Text($"Root Actor: {root.Name}");
                ImGui.Separator();

                for (var i = 0; i < root.Children.Count; i++)
                {
                    var child = root.Children[i];

                    ImGui.PushID($"{child.Name}_{i}");
                    if (ImGui.TreeNode(child.Name))
                    {
                        ImGui.Text($"Position: {child.Transform.Position}");
                        ImGui.Text($"Rotation: {child.Transform.Rotation}");
                        ImGui.Text($"Scale: {child.Transform.Scale}");
                        ImGui.Text($"Components: {child.Components.Count}");
                        foreach (var component in child.Components)
                        {
                            ImGui.Text($"- {component.GetType().Name}");
                        }
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }

                if (ImGui.Button("Add Actor"))
                {
                    var actor = new Actor("New Actor");
                    actor.Components.Add(new PrimitiveComponent(new Cube()));
                    var forwardVector = Vector3.Transform(Vector3.UnitZ, camera?.Actor?.Transform.Rotation ?? Quaternion.Identity);
                    actor.Transform.Position = (camera?.Actor?.Transform.Position ?? Vector3.Zero) + forwardVector * 3;
                    root.Children.Add(actor);
                }
            }
            ImGui.End();
        }

        if (ImGui.Begin("Systems Order"))
        {
            ImGui.Checkbox("Debug Mode", ref _sceneSystem.DebugMode);
            foreach (var system in _sceneSystem.Systems)
            {
                ImGui.Text($"- {system.Value.GetType().Name} (Priority: {system.Key}) (Components: x{system.Value.ComponentsCount})");
            }
        }
        ImGui.End();

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
