using System.Numerics;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
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

namespace Snooper;

public partial class SnooperWindow : GameWindow
{
    private readonly Actor _scene = new("Scene");
    private readonly SceneSystem _sceneSystem;
    private readonly ImGuiSystem _imguiSystem;
    
    public SnooperWindow(double fps, int width, int height, bool startVisible = true) : base(
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
            Title = "Snooper"
        })
    {
        ActorManager.RegisterSystemFactory<GridSystem>();
        ActorManager.RegisterSystemFactory<TransformSystem>();
        ActorManager.RegisterSystemFactory<CameraSystem>();
        ActorManager.RegisterSystemFactory<CullingSystem>();
        ActorManager.RegisterSystemFactory<PrimitiveSystem>();
        ActorManager.RegisterSystemFactory<DeferredRenderSystem>();
        ActorManager.RegisterSystemFactory<RenderSystem>();
        ActorManager.RegisterSystemFactory<DebugSystem>();
        
        var grid = new Actor("Grid");
        grid.Components.Add(new GridComponent());
        _scene.Children.Add(grid);
        
        var camera = new Actor("Camera");
        camera.Transform.Position -= Vector3.UnitZ * 5;
        camera.Transform.Position += Vector3.UnitY * 1.5f;
        camera.Components.Add(new CameraComponent());
        _scene.Children.Add(camera);
        
        _sceneSystem = new SceneSystem(this);
        _sceneSystem.RootActor = _scene;
        
        _imguiSystem = new ImGuiSystem();
        _imguiSystem.Resize(ClientSize.X, ClientSize.Y);
    }

    public void AddToScene(UObject actor) => AddToScene(actor, FTransform.Identity);
    public void AddToScene(UObject actor, FTransform transform)
    {
        switch (actor)
        {
            case UStaticMesh staticMesh:
            {
                var mesh = new MeshActor(staticMesh, transform);
                _scene.Children.Add(mesh);
                break;
            }
            case USkeletalMesh skeletalMesh:
            {
                var mesh = new MeshActor(skeletalMesh, transform);
                _scene.Children.Add(mesh);
                break;
            }
            default:
                throw new NotImplementedException($"Actor type {actor.GetType()} is not supported.");
        }
    }
    
    protected override void OnLoad()
    {
        base.OnLoad();

        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Front);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // GL.Enable(EnableCap.VertexProgramPointSize);
        // GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);
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

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.ClearColor(0, 1, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        ImGui.DockSpaceOverViewport();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        foreach (var pair in _sceneSystem.Pairs)
        {
            if (ImGui.Begin($"Viewport ({pair.Camera.Actor?.Name})"))
            {
                if (ImGui.IsWindowFocused()) _sceneSystem.ActiveCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var pointers = pair.GetPointers();
                var size = new Vector2(largest.X, largest.Y);
                pair.Camera.ViewportSize = size;
                ImGui.Image(pointers[^1], size, Vector2.UnitY, Vector2.UnitX);

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
                var frameHeight = ImGui.GetFrameHeight();

                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMin();

                if (_sceneSystem.DebugMode)
                {
                    var remainingPointers = pointers.Length - 1;
                    var miniSize = size;
                    miniSize.Y = MathF.Min(miniSize.Y, (size.Y - margin) / remainingPointers) - frameHeight;
                    miniSize.X = miniSize.Y * (size.X / size.Y);
                    // if the size is greater than 1/3 of the viewport, we will clamp it to 1/3
                    if (miniSize.X > size.X / 3.0f)
                    {
                        miniSize.X = size.X / 3.0f;
                        miniSize.Y = miniSize.X * (size.Y / size.X);
                    }

                    var topRight = new Vector2(pos.X + size.X - miniSize.X - margin, pos.Y + margin);
                    for (int i = 0; i < remainingPointers; i++)
                    {
                        var pMin = topRight with { Y = topRight.Y + i * (miniSize.Y + margin) };
                        var pMax = pMin + miniSize;

                        drawList.AddImage(pointers[i], pMin, pMax, Vector2.UnitY, Vector2.UnitX);
                        drawList.AddRect(pMin, pMax, ImGui.GetColorU32(ImGuiCol.Border));
                    }
                }

                drawList.AddText(new Vector2(pos.X + margin, pos.Y + margin), ImGui.GetColorU32(ImGuiCol.Text), $"Primitives: {primitiveCount:N0}");

                var framerate = ImGui.GetIO().Framerate;
                drawList.AddText(
                    new Vector2(pos.X + margin, pos.Y + size.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.Text),
                    $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)"
                );

                const string label = "Previewed content may differ from final version saved or used in-game.";
                drawList.AddText(
                    new Vector2(pos.X + size.X - ImGui.CalcTextSize(label).X - margin, pos.Y + size.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.TextDisabled),
                    label
                );
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

                ImGui.Checkbox("FXAA", ref camera.bFXAA);
                ImGui.Checkbox("SSAO", ref camera.bSSAO);
                ImGui.BeginDisabled(!camera.bSSAO);
                ImGui.SliderFloat("Radius", ref camera.SsaoRadius, 0.01f, 1.0f);
                ImGui.SliderFloat("Bias", ref camera.SsaoBias, 0.0f, 0.1f);
                ImGui.EndDisabled();

                ImGui.DragFloat("Near Plane", ref camera.NearPlaneDistance, 0.001f, 0.001f, 0.099f);
                ImGui.DragFloat("Far Plane", ref camera.FarPlaneDistance, 0.1f , camera.NearPlaneDistance, 1000.0f);
            }
            ImGui.End();
        }

        if (ImGui.Begin("Scene Hierarchy"))
        {
            for (var i = 0; i < _scene.Children.Count; i++)
            {
                var child = _scene.Children[i];

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

            if (ImGui.BeginPopupContextWindow("Add Actor"))
            {
                if (ImGui.MenuItem("Add Cube"))
                {
                    var cube = new Actor("Cube");
                    cube.Components.Add(new PrimitiveComponent(new Cube()));
                    cube.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

                    var forwardVector = Vector3.Transform(Vector3.UnitZ, camera?.Actor?.Transform.Rotation ?? Quaternion.Identity);
                    cube.Transform.Position = (camera?.Actor?.Transform.Position ?? Vector3.Zero) + forwardVector * 3;
                    _scene.Children.Add(cube);
                }
                if (ImGui.MenuItem("Add Sphere"))
                {
                    var sphere = new Actor("Sphere");
                    sphere.Components.Add(new PrimitiveComponent(new Sphere(18, 9, 0.5f)));
                    sphere.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

                    var forwardVector = Vector3.Transform(Vector3.UnitZ, camera?.Actor?.Transform.Rotation ?? Quaternion.Identity);
                    sphere.Transform.Position = (camera?.Actor?.Transform.Position ?? Vector3.Zero) + forwardVector * 3;
                    _scene.Children.Add(sphere);
                }
                if (ImGui.MenuItem("Add Camera"))
                {
                    var cameraActor = new CameraActor("Camera Added");
                    _scene.Children.Add(cameraActor);
                }
                ImGui.EndPopup();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Systems Order"))
        {
            ImGui.Checkbox("Debug Mode", ref _sceneSystem.DebugMode);
            foreach (var system in _sceneSystem.Systems.GroupBy(x => x.Value.SystemType).OrderByDescending(x => x.Key))
            {
                ImGui.Text($"{system.Key}");
                foreach (var pair in system)
                {
                    ImGui.Text($"- {pair.Value.GetType().Name} (Priority: {pair.Key}, Components: x{pair.Value.ComponentsCount})");
                }
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
            pair.Resize(e.Width, e.Height);
        _imguiSystem.Resize(e.Width, e.Height);
    }
}