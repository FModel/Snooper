using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Systems;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;

namespace Snooper.UI.Systems;

public class ImGuiSystem(GameWindow wnd) : IInterfaceSystem
{
    public void Render(SceneSystem scene)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        ImGui.DockSpaceOverViewport();
        
        ImGui.ShowDemoWindow();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        foreach (var pair in scene.Pairs)
        {
            if (ImGui.Begin($"Viewport ({pair.Camera.Actor?.Name})", ref pair.IsOpen))
            {
                if (ImGui.IsWindowFocused()) scene.ActiveCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var framebuffers = pair.GetFramebuffers();
                var size = new Vector2(largest.X, largest.Y);
                pair.Camera.ViewportSize = size;
                ImGui.Image(framebuffers[^1], size, Vector2.UnitY, Vector2.UnitX);

                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    wnd.CursorState = CursorState.Grabbed;
                }

                const float margin = 7.5f;
                var frameHeight = ImGui.GetFrameHeight();

                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMin();

                if (scene.DebugMode)
                {
                    var remainingPointers = framebuffers.Length - 1;
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
                    for (var i = 0; i < remainingPointers; i++)
                    {
                        var pMin = topRight with { Y = topRight.Y + i * (miniSize.Y + margin) };
                        var pMax = pMin + miniSize;

                        drawList.AddImage(framebuffers[i], pMin, pMax, Vector2.UnitY, Vector2.UnitX);
                        drawList.AddRect(pMin, pMax, ImGui.GetColorU32(ImGuiCol.Border));
                    }
                }

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

        var camera = scene.ActiveCamera;
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

                ImGui.DragFloat("Speed", ref camera.MovementSpeed, 0.1f, 1f, 100f);
                ImGui.DragFloat("Near Plane", ref camera.NearPlaneDistance, 0.001f, 0.001f, camera.FarPlaneDistance - 1);
                ImGui.DragFloat("Far Plane", ref camera.FarPlaneDistance, 0.1f , camera.NearPlaneDistance + 1, 1000.0f);
            }
            ImGui.End();
        }

        if (scene.RootActor is { } root && ImGui.Begin("Scene Hierarchy"))
        {
            foreach (var child in root.Children)
            {
                child.DrawInterface();
            }

            if (ImGui.BeginPopupContextWindow("Add Actor"))
            {
                if (ImGui.MenuItem("Add Cube"))
                {
                    var cube = new Actor(Guid.NewGuid(), "Cube");
                    cube.Components.Add(new PrimitiveComponent(new Cube()));
                    cube.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

                    var forwardVector = Vector3.Transform(Vector3.UnitZ, camera?.Actor?.Transform.Rotation ?? Quaternion.Identity);
                    cube.Transform.Position = (camera?.Actor?.Transform.Position ?? Vector3.Zero) + forwardVector * 3;
                    root.Children.Add(cube);
                }
                if (ImGui.MenuItem("Add Sphere"))
                {
                    var sphere = new Actor(Guid.NewGuid(), "Sphere");
                    sphere.Components.Add(new PrimitiveComponent(new Sphere(18, 9, 0.5f)));
                    sphere.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

                    var forwardVector = Vector3.Transform(Vector3.UnitZ, camera?.Actor?.Transform.Rotation ?? Quaternion.Identity);
                    sphere.Transform.Position = (camera?.Actor?.Transform.Position ?? Vector3.Zero) + forwardVector * 3;
                    root.Children.Add(sphere);
                }
                if (ImGui.MenuItem("Add Camera"))
                {
                    var cameraActor = new CameraActor($"Camera {scene.Pairs.Count + 1}");
                    root.Children.Add(cameraActor);
                }
                ImGui.EndPopup();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Profiler"))
        {
            ImGui.Text($"API: {scene.Context.Name}");
            ImGui.Text($"OpenGL: {scene.Context.Version}");
            ImGui.Text($"GPU: {scene.Context.DeviceInfo.Name}");
            ImGui.Text($"Vendor: {scene.Context.DeviceInfo.Vendor}");
            ImGui.Text($"Extensions: x{scene.Context.DeviceInfo.ExtensionSupport.Extensions.Length}");
            
            ImGui.SeparatorText("Options");
            ImGui.Checkbox("Debug Mode", ref scene.DebugMode);
            ImGui.Checkbox("Draw Bounding Boxes", ref scene.DrawBoundingBoxes);
            var c = (int) scene.DebugColorMode;
            ImGui.Combo("DebugColorMode", ref c, "None\0Per Actor\0Per Instance\0Per Material\0Per Primitive\0");
            scene.DebugColorMode = (ActorDebugColorMode) c;
            
            ImGui.SeparatorText("Systems");
            foreach (var system in scene.Systems.Values)
            {
                var name = system.GetType().Name;
                if (ImGui.CollapsingHeader($"{system.Order} - {name} ({system.SystemType})"))
                {
                    if (ImGui.TreeNode($"x{system.ComponentsCount} {system.ComponentType?.Name}{(system.ComponentsCount > 1 ? "s" : "")}"))
                    {
                        if (system is IMemorySizeProvider provider)
                        {
                            ImGui.TextUnformatted(provider.GetFormattedSpace());
                        }
                        ImGui.TreePop();
                    }
                    
                    if (ImGui.TreeNode($"{name}_profiler", "Profiler"))
                    {
                        system.Profiler.PollResults();

                        ImGui.Text($"Primitives Generated: {system.Profiler.PrimitivesGenerated:N0}");
                        ImGui.PlotLines(
                            "Time Elapsed (ms)", ref system.Profiler.TimeElapsedMs[0],
                            SystemProfiler.MaxFrameHistory, 0, $"avg {system.Profiler.AverageTimeElapsedMs:F} ms",
                            0, system.Profiler.MaxTimeElapsedMs,
                            new Vector2(0, 25));
                        
                        ImGui.TreePop();
                    }
                }
            }
        }
        ImGui.End();

        _controller.Render();
    }
    
    private readonly ImGuiController _controller = new();

    public void Load()
    {
        _controller.Load();
        IsActive = true;
    }
    public void Update(float delta) => _controller.Update(wnd, delta);
    public void Resize(int newWidth, int newHeight) => _controller.Resize(newWidth, newHeight);
    public bool IsActive { get; set; }
    public void TextInput(char c) => _controller.TextInput(c);

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}