using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Culling;
using Snooper.Rendering.Primitives;

namespace Snooper.UI.Systems;

public class LevelSystem(GameWindow wnd) : InterfaceSystem(wnd)
{
    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();
        
        ImGui.ShowDemoWindow();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        foreach (var pair in Pairs)
        {
            if (ImGui.Begin($"Viewport ({pair.Camera.Actor?.Name})", ref pair.IsOpen))
            {
                if (ImGui.IsWindowFocused()) ActiveCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var framebuffers = pair.GetFramebuffers();
                var size = new Vector2(largest.X, largest.Y);
                pair.Camera.ViewportSize = size;
                ImGui.Image(framebuffers[^1], size, Vector2.UnitY, Vector2.UnitX);

                if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    Window.CursorState = CursorState.Grabbed;
                }

                const float margin = 7.5f;
                var frameHeight = ImGui.GetFrameHeight();

                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMin();

                if (DebugMode)
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
                
                const string label1 = "Press F10 to toggle interface";
                drawList.AddText(
                    new Vector2(pos.X + size.X - ImGui.CalcTextSize(label1).X - margin, pos.Y + margin),
                    ImGui.GetColorU32(ImGuiCol.Text),
                    label1
                );

                var framerate = ImGui.GetIO().Framerate;
                drawList.AddText(
                    new Vector2(pos.X + margin, pos.Y + size.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.Text),
                    $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)"
                );

                const string label2 = "Previewed content may differ from final version saved or used in-game.";
                drawList.AddText(
                    new Vector2(pos.X + size.X - ImGui.CalcTextSize(label2).X - margin, pos.Y + size.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.TextDisabled),
                    label2
                );
            }
            ImGui.End();
        }
        ImGui.PopStyleVar();

        if (RootActor is { } root && ImGui.Begin("Scene Hierarchy"))
        {
            foreach (var child in root.Children)
                DrawActorTree(child);
            
            if (ImGui.BeginPopupContextWindow("SceneContext", ImGuiPopupFlags.MouseButtonRight))
            {
                DrawActorCreationMenu(root);
                ImGui.EndPopup();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Profiler"))
        {
            ImGui.Text($"API: {Context.Name}");
            ImGui.Text($"OpenGL: {Context.Version}");
            ImGui.Text($"GPU: {Context.DeviceInfo.Name}");
            ImGui.Text($"Vendor: {Context.DeviceInfo.Vendor}");
            ImGui.Text($"Extensions: x{Context.DeviceInfo.ExtensionSupport.Extensions.Length}");
            
            ImGui.SeparatorText("Options");
            ImGui.Checkbox("Debug Mode", ref DebugMode);
            ImGui.Checkbox("Draw Bounding Boxes", ref DrawBoundingBoxes);
            var c = (int) DebugColorMode;
            ImGui.Combo("DebugColorMode", ref c, "None\0Per Actor\0Per Instance\0Per Material\0Per Primitive\0");
            DebugColorMode = (ActorDebugColorMode) c;
            
            ImGui.SeparatorText("Systems");
            foreach (var system in Systems.Values)
            {
                var name = system.GetType().Name;
                if (ImGui.CollapsingHeader($"{system.Order} - {name} ({system.SystemType})"))
                {
                    ImGui.TextUnformatted($"Time: {system.Time:F3} s");
                    
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

        if (ImGui.Begin("Inspector"))
        {
            DrawActorInspector();
        }
        ImGui.End();
    }

    private Actor? _selectedActor;
    private readonly Vector2 _iconSize = new(20);
    
    private void DrawActorTree(Actor actor)
    {
        ImGui.PushID(actor.Id);
        
        var count = actor.Children.Count;
        var isSelected = actor == _selectedActor;
        var flags = ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
        if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (count == 0) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        
        ImGui.AlignTextToFramePadding();
        var open = ImGui.TreeNodeEx("##tree", flags);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            _selectedActor = actor;
        
        ImGui.SameLine();
        if (Icons.TryGetValue(actor.Icon, out var icon))
            ImGui.Image(icon.GetPointer(), _iconSize, Vector2.UnitX, Vector2.UnitY);
        else
            ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
        ImGui.SameLine();
        ImGui.TextUnformatted(actor.Name);
        
        if (open && count > 0)
        {
            foreach (var child in actor.Children)
                DrawActorTree(child);
            
            ImGui.TreePop();
        }
        
        ImGui.PopID();
    }

    private void DrawActorInspector()
    {
        if (_selectedActor is null)
        {
            ImGui.TextUnformatted("No actor selected.");
            return;
        }
        
        ImGui.Text(_selectedActor.Name);
        ImGui.Text($"Visible Instances: {_selectedActor.VisibleInstances}");
        ImGui.Text($"Instances: {_selectedActor.InstancedTransform.Transforms.Count + 1}");

        foreach (var component in _selectedActor.Components)
            component.DrawInterface();
    }
    
    private void DrawActorCreationMenu(Actor parent)
    {
        if (ImGui.MenuItem("Add Cube"))
        {
            var cube = new Actor("Cube");
            cube.Components.Add(new PrimitiveComponent(new Cube()));
            cube.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

            PlaceInFrontOfCamera(cube);
            parent.Children.Add(cube);
        }

        if (ImGui.MenuItem("Add Sphere"))
        {
            var sphere = new Actor("Sphere");
            sphere.Components.Add(new PrimitiveComponent(new Sphere(18, 9, 0.5f)));
            sphere.Components.Add(new BoxCullingComponent(Vector3.Zero, Vector3.One / 2));

            PlaceInFrontOfCamera(sphere);
            parent.Children.Add(sphere);
        }

        if (ImGui.MenuItem("Add Camera"))
        {
            var camera = new CameraActor($"Camera {Pairs.Count + 1}");
            PlaceInFrontOfCamera(camera);
            parent.Children.Add(camera);
        }
    }
    
    private void PlaceInFrontOfCamera(Actor actor)
    {
        if (ActiveCamera?.Actor is { Transform: var camTransform })
        {
            var forward = Vector3.Transform(Vector3.UnitZ, camTransform.Rotation);
            actor.Transform.Position = camTransform.Position + forward * 3f;
        }
    }
}