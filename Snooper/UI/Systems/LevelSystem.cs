using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;

namespace Snooper.UI.Systems;

public class LevelSystem(GameWindow wnd) : InterfaceSystem(wnd)
{
    private bool _once;
    
    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();
        
        Notifications.DrawNotifications();
        if (!_once)
        {
            NotifyOnFirstRender();
            _once = true;
        }

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
                DrawAtOrigin(SelectedComponent, pair.Camera, ImGui.GetWindowDrawList(), 8f, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));

                if (ImGui.IsItemHovered())
                {
                    if (Window.MouseState.ScrollDelta.Y != 0)
                    {
                        var multiplier = Window.KeyboardState.IsKeyDown(Keys.LeftShift) ? 5 : 1f;
                        pair.Camera.MovementSpeed += Window.MouseState.ScrollDelta.Y * multiplier;
                        pair.Camera.MovementSpeed = MathF.Max(1f, pair.Camera.MovementSpeed);
                        Notifications.PushNotification("Camera", $"Movement speed set to {pair.Camera.MovementSpeed}.");
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        Window.CursorState = CursorState.Grabbed;
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        SelectedComponentId = pair.ReadPickingPixel(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), size);
                        ImGui.SetWindowFocus("Scene Hierarchy");
                    }
                }

                const float margin = 7.5f;
                var frameHeight = ImGui.GetFrameHeight();

                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMin();

                if (ShowFramebuffers)
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
                
                ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiSemiBold]);

                var framerate = ImGui.GetIO().Framerate;
                drawList.AddText(
                    new Vector2(pos.X + margin, pos.Y + size.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.Text),
                    $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)"
                );

                var col = ImGui.GetColorU32(new Vector4(1.00f, 1.00f, 1.00f, 0.50f));
                const string label1 = "F10: Toggle UI | LMB: Select Object | RMB: Move Camera | Scroll: Adjust Speed";
                drawList.AddText(
                    new Vector2(pos.X + size.X - ImGui.CalcTextSize(label1).X - margin, pos.Y + margin),
                    col, label1
                );

                const string label2 = "Previewed content may differ from final version saved or used in-game.";
                drawList.AddText(
                    new Vector2(pos.X + size.X - ImGui.CalcTextSize(label2).X - margin, pos.Y + size.Y - frameHeight),
                    col, label2
                );
                
                ImGui.PopFont();
            }
            ImGui.End();
        }
        ImGui.PopStyleVar();

        if (ImGui.Begin("Scene Hierarchy"))
        {
            if (RootActor is { } root)
            {
                foreach (var child in root.Children)
                    DrawActorTree(child);
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
            ImGui.Checkbox("Show Framebuffers", ref ShowFramebuffers);
            var c = (int) DebugColorMode;
            ImGui.Combo("DebugColorMode", ref c, "None\0Per Component\0Per Instance\0Per Material\0Per Primitive\0");
            DebugColorMode = (ActorDebugColorMode) c;
            
            ImGui.SeparatorText("Systems");
            foreach (var system in Systems.Values)
            {
                if (ImGui.CollapsingHeader($"{system.Order} - {system.DisplayName} ({system.SystemType})"))
                {
                    ImGui.TextUnformatted($"Time: {system.Time:F3} s");
                    
                    if (ImGui.TreeNode($"x{system.ComponentsCount} {system.ComponentType.Name}{(system.ComponentsCount > 1 ? "s" : "")}"))
                    {
                        if (system is IMemorySizeProvider provider)
                        {
                            ImGui.TextUnformatted(provider.GetFormattedSpace());
                        }
                        ImGui.TreePop();
                    }
                    
                    if (ImGui.TreeNode($"{system.DisplayName}_profiler", "Profiler"))
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

                    if (system is IControllable controllable)
                    {
                        if (ImGui.TreeNode($"{system.DisplayName}_controls", "Controls"))
                        {
                            controllable.DrawControls();
                            ImGui.TreePop();
                        }
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
    
    private void DrawAtOrigin(
        ActorComponent? component,
        CameraComponent? camera,
        ImDrawListPtr drawList,
        float rectSize = 16f,
        Vector4? color = null)
    {
        if (component is not SpatialComponent spatial || camera == null) return;

        foreach (var matrix in spatial.GetInstanceMatrices())
        {
            var clip = Vector4.Transform(new Vector4(matrix.Translation, 1f), camera.ViewProjectionMatrix);
            if (clip.W <= 0) continue;
        
            clip /= clip.W;
            var ndc = new Vector2(clip.X, clip.Y);
            var screenPos = new Vector2((ndc.X + 1f) * 0.5f * camera.ViewportSize.X, (1f - ndc.Y) * 0.5f * camera.ViewportSize.Y + ImGui.GetFrameHeight());
            var pMin = screenPos - new Vector2(rectSize / 2f);
            var pMax = screenPos + new Vector2(rectSize / 2f);

            drawList.AddRect(pMin, pMax, ImGui.GetColorU32(color ?? new Vector4(1f, 1f, 0f, 1f)), 0f, ImDrawFlags.None, 2f);
        }
    }

    private void NotifyOnFirstRender()
    {
        var systems = Systems.Values.OfType<ITexturedSystem>().Where(s => s.TextureManager.IsLoadingTextures).ToArray();
        if (systems.Length == 0)
            return;

        foreach (var system in systems)
        {
            Notifications.PushNotification("Loading textures, please wait...", $"{system.GetType().Name}: {system.TextureManager.NumberOfTexturesToLoad} textures", 1, () => system.TextureManager.LoadingProgress);
        }
    }

    private readonly Vector2 _iconSize = new(20);
    
    private void DrawActorTree(Actor actor)
    {
        ImGui.PushID(actor.Id);
        
        var count = actor.Children.Count;
        var flags = ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
        if (actor.IsSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (count == 0) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        
        ImGui.AlignTextToFramePadding();
        var open = ImGui.TreeNodeEx("##tree", flags);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            SelectedComponentId = actor.RootComponent?.Id ?? 0;
        }
        
        ImGui.SameLine();
        if (Icons.TryGetValue(actor.Icon, out var icon))
            ImGui.Image(icon.GetPointer(), _iconSize);
        else
            ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
        ImGui.SameLine();

        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiSemiBold]);
        ImGui.TextUnformatted(actor.Name);
        ImGui.PopFont();
        
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
        if (SelectedComponent is not { } component)
        {
            ImGui.TextUnformatted("No component selected.");
            return;
        }
        
        if (component.Actor is not { } actor)
        {
            ImGui.TextUnformatted("No actor selected.");
            return;
        }

        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiBold]);
        ImGui.TextUnformatted(actor.Name);
        ImGui.PopFont();
        if (actor.ExportType != null)
        {
            ImGui.Text($"Export Type: {actor.ExportType}");
        }
        if (actor.InternalType != null)
        {
            ImGui.Text($"Internal Type: {actor.InternalType}");
        }

        var components = actor.Components;
        if (components.Count == 0)
        {
            ImGui.TextUnformatted("This actor has no components.");
            return;
        }

        ImGui.SeparatorText($"{components.Count} Component{(components.Count > 1 ? "s" : "")}");
        
        if (Icons.TryGetValue(component.Icon, out var icon))
            ImGui.Image(icon.GetPointer(), _iconSize);
        else
            ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
        ImGui.SameLine();ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.BeginCombo("##Components", component.Name))
        {
            foreach (var c in components)
            {
                if (Icons.TryGetValue(c.Icon, out icon))
                    ImGui.Image(icon.GetPointer(), _iconSize * 0.75f);
                else
                    ImGui.Image(0, _iconSize, Vector2.UnitX, Vector2.UnitY, Vector4.One, Vector4.One);
                ImGui.SameLine();
                
                var selected = c.Id == SelectedComponentId;
                if (ImGui.Selectable(c.Name, selected))
                {
                    SelectedComponentId = c.Id;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        
        component.DrawInterface();
    }
}