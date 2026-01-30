using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core.Containers;
using Snooper.Core.Systems;
using Snooper.Extensions;
using Snooper.Rendering;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;

namespace Snooper.UI.Systems;

public class LevelSystem(GameWindow wnd) : InterfaceSystem(wnd)
{
    private bool _firstRender;
    private bool _scrollToSelected;
    private int _selectedCameraIndex;

    protected override void RenderInterface()
    {
        ImGui.DockSpaceOverViewport();

        Notifications.DrawNotifications();
        if (!_firstRender)
        {
            NotifyOnFirstRender();
            _firstRender = true;
        }

        ImGui.ShowDemoWindow();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin("Viewport"))
        {
            if (Pairs.Count > 0)
            {
                if (_selectedCameraIndex >= Pairs.Count)
                    _selectedCameraIndex = 0;

                var pair = Pairs[_selectedCameraIndex];
                if (ImGui.IsWindowFocused()) ActiveCamera = pair.Camera;

                var largest = ImGui.GetContentRegionAvail();
                largest.X -= ImGui.GetScrollX();
                largest.Y -= ImGui.GetScrollY();

                var framebuffers = pair.GetTextures();
                var viewportSize = new Vector2(largest.X, largest.Y);
                pair.Camera.Resize((int)viewportSize.X, (int)viewportSize.Y);
                ImGui.Image(framebuffers[^1].GetPointer(), viewportSize, Vector2.UnitY, Vector2.UnitX);

                if (ImGui.IsItemHovered())
                {
                    if (wnd.MouseState.ScrollDelta.Y != 0)
                    {
                        var multiplier = wnd.KeyboardState.IsKeyDown(Keys.LeftShift) ? 5 : 1f;
                        pair.Camera.MovementSpeed += wnd.MouseState.ScrollDelta.Y * multiplier;
                        pair.Camera.MovementSpeed = MathF.Max(1f, pair.Camera.MovementSpeed);
                        Notifications.PushNotification("Camera", () => $"Movement speed set to {pair.Camera.MovementSpeed}.");
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        wnd.CursorState = CursorState.Grabbed;
                    }

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        var componentId = pair.ReadPickingPixel(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), viewportSize);
                        SelectedActor = null;
                        SelectedComponent = FindComponentById(componentId);
                        _scrollToSelected = true;
                        ImGui.SetWindowFocus("Outliner");
                    }

                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ActiveCamera != null && SelectedComponent is SpatialComponent spatial)
                    {
                        var (center, distance) = spatial.GetTeleportPosition(ActiveCamera);
                        ActiveCamera.TeleportTo(center + ActiveCamera.Forward * distance);
                    }
                }

                const float margin = 7.5f;
                var frameHeight = ImGui.GetFrameHeight();

                var drawList = ImGui.GetWindowDrawList();
                var pos = ImGui.GetItemRectMin();

                DrawCameraControls(pair.Camera, pos, margin);

                if (ShowFramebuffers)
                {
                    var remainingPointers = framebuffers.Length - 1;
                    var miniSize = viewportSize;
                    miniSize.Y = MathF.Min(miniSize.Y, (viewportSize.Y - margin) / remainingPointers) - frameHeight;
                    miniSize.X = miniSize.Y * (viewportSize.X / viewportSize.Y);
                    // if the size is greater than 1/3 of the viewport, we will clamp it to 1/3
                    if (miniSize.X > viewportSize.X / 3.0f)
                    {
                        miniSize.X = viewportSize.X / 3.0f;
                        miniSize.Y = miniSize.X * (viewportSize.Y / viewportSize.X);
                    }

                    var topRight = new Vector2(pos.X + viewportSize.X - miniSize.X - margin, pos.Y + margin);
                    for (var i = 0; i < remainingPointers; i++)
                    {
                        var pMin = topRight with { Y = topRight.Y + i * (miniSize.Y + margin) };
                        var pMax = pMin + miniSize;

                        drawList.AddImage(framebuffers[i].GetPointer(), pMin, pMax, Vector2.UnitY, Vector2.UnitX);
                        drawList.AddRect(pMin, pMax, ImGui.GetColorU32(ImGuiCol.Border));
                    }
                }

                ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiSemiBold]);

                var framerate = ImGui.GetIO().Framerate;
                drawList.AddText(
                    new Vector2(pos.X + margin, pos.Y + viewportSize.Y - frameHeight),
                    ImGui.GetColorU32(ImGuiCol.Text),
                    $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms)"
                );

                const string label = "Previewed content may differ from final version saved or used in-game.";
                drawList.AddText(
                    new Vector2(pos.X + viewportSize.X - ImGui.CalcTextSize(label).X - margin, pos.Y + viewportSize.Y - frameHeight),
                    ImGui.GetColorU32(new Vector4(1.00f, 1.00f, 1.00f, 0.50f)),
                    label
                );

                ImGui.PopFont();
            }
            else
            {
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f));
                ImGui.TextUnformatted("No camera available.");
                ImGui.PopStyleVar();
            }
        }
        ImGui.End();
        ImGui.PopStyleVar();

        if (ImGui.Begin("Outliner"))
        {
            if (RootActor is { } root && root.Children.ToList() is { Count: > 0 } children)
            {
                foreach (var child in children)
                    DrawActorTree(child, true);
            }
        }
        ImGui.End();

        if (ImGui.Begin("Profiler"))
        {
            if (ImGui.BeginTabBar("ProfilerTabs"))
            {
                if (ImGui.BeginTabItem("Overview"))
                {
                    ImGui.Columns(2, "SysInfo", false);
                    ImGui.Text($"API: {Context.Name}");
                    ImGui.Text($"GPU: {Context.DeviceInfo.Name}");
                    ImGui.NextColumn();
                    ImGui.Text($"OpenGL: {Context.Version}");
                    ImGui.Text($"Vendor: {Context.DeviceInfo.Vendor}");
                    ImGui.Columns(1);

                    ImGui.Spacing();
                    ImGui.SeparatorText("Thread Manager");

                    ImGui.Columns(3, "ThreadInfo", false);
                    ImGui.Text($"Workers: {ThreadManager.WorkerCount}");
                    ImGui.Text($"Queued Jobs: {ThreadManager.CurrentQueuedJobs}");
                    ImGui.NextColumn();
                    ImGui.Text($"Jobs Processed: {ThreadManager.TotalJobsProcessed:N0}");
                    ImGui.Text($"Jobs Enqueued: {ThreadManager.TotalJobsEnqueued:N0}");
                    ImGui.NextColumn();
                    ImGui.Text($"Avg Job Time: {ThreadManager.AverageJobTimeMs.FormatTime()}");
                    ImGui.Text($"Max Job Time: {ThreadManager.MaxJobTimeMs.FormatTime()}");
                    ImGui.Columns(1);

                    if (ImGui.TreeNode("Worker Threads"))
                    {
                        if (ImGui.BeginTable("WorkerTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
                        {
                            ImGui.TableSetupColumn("Name");
                            ImGui.TableSetupColumn("Status");
                            ImGui.TableSetupColumn("Queue");
                            ImGui.TableSetupColumn("Jobs Processed");
                            ImGui.TableSetupColumn("Avg Time (ms)");
                            ImGui.TableSetupColumn("Max Time (ms)");
                            ImGui.TableHeadersRow();

                            var workerStats = ThreadManager.GetWorkerStats();
                            foreach (var worker in workerStats)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.TextUnformatted(worker.Name);
                                ImGui.TableNextColumn();
                                if (worker.IsIdle)
                                {
                                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Idle");
                                }
                                else
                                {
                                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Working");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{worker.QueueLength}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{worker.JobsProcessed:N0}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{worker.AverageJobTimeMs:F3}");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{worker.MaxJobTimeMs:F3}");
                            }

                            ImGui.EndTable();
                        }
                        ImGui.TreePop();
                    }

                    ImGui.Spacing();
                    ImGui.SeparatorText("GPU Memory");
                    MemoryDetailsUI.DrawMemorySummary(this);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Memory"))
                {
                    MemoryDetailsUI.DrawMemoryTable(this, Icons);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Systems"))
                {
                    foreach (var system in Systems.Values)
                    {
                        if (ImGui.CollapsingHeader($"{system.Order}. {system.DisplayName}"))
                        {
                            ImGui.Columns(2, $"SysTable{system.Order}", false);
                            {
                                ImGui.TextDisabled("Time");
                                ImGui.TextUnformatted($"{system.Time:F2} s");
                                ImGui.Spacing();
                                ImGui.TextDisabled("Is Enabled");
                                ImGui.Checkbox($"##Enabled{system.Order}", ref system.IsEnabled);

                                ImGui.NextColumn();
                                ImGui.TextDisabled("Components");
                                ImGui.TextUnformatted($"{system.ComponentsCount:N0} {system.ComponentType.Name}{(system.ComponentsCount > 1 ? "s" : "")}");
                                ImGui.Spacing();
                                system.Profiler.PollResults();
                                ImGui.TextDisabled("Primitives");
                                ImGui.TextUnformatted($"{system.Profiler.PrimitivesGenerated:N0}");

                                ImGui.NextColumn();
                                ImGui.TextDisabled("Show Wireframe");
                                ImGui.Checkbox($"##ShowWireframe{system.Order}", ref system.ShowWireframe);
                            }
                            ImGui.Columns(1);

                            if (system is IMemorySizeProvider provider)
                            {
                                ImGui.Spacing();
                                MemoryDetailsUI.DrawMemorySummary(provider);
                            }

                            if (ImGui.TreeNode($"Performance Metrics##SysMetrics{system.Order}"))
                            {
                                MemoryDetailsUI.DrawPerformanceMetrics(system.Profiler, system.Order.ToString());
                                ImGui.TreePop();
                            }

                            if (system is IControllable controllable)
                            {
                                if (ImGui.TreeNode($"Controls##SysControls{system.Order}"))
                                {
                                    controllable.DrawControls();
                                    ImGui.TreePop();
                                }
                            }
                        }
                    }
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Inspector"))
        {
            DrawActorInspector();
        }
        ImGui.End();

        if (ImGui.Begin("Content Browser"))
        {

        }
        ImGui.End();
        if (ImGui.Begin("Timeline"))
        {

        }
        ImGui.End();
        LogWindow.Draw();

        TexturePreviewWindow.DrawAll();
    }

    private void NotifyOnFirstRender()
    {
        var systems = Systems.Values.OfType<ITexturedSystem>().Where(s => s.TextureManager.IsLoading).ToArray();
        if (systems.Length == 0)
            return;

        foreach (var system in systems)
        {
            Notifications.PushNotification($"{system.GetType().Name}: Textures", () => $"Loading {system.TextureManager.PendingTextureCount} textures, please wait...", 1, () => system.TextureManager.LoadingProgress);
        }
    }

    private readonly Vector2 _iconSize = new(18);
    private readonly Vector2 _actionIconSize = new(16);

    private bool HasSelectedDescendant(Actor actor)
    {
        if (actor.IsSelected) return true;
        foreach (var child in actor.Children)
            if (HasSelectedDescendant(child)) return true;
        return false;
    }

    private void DrawActorTree(Actor actor, bool clip = false)
    {
        ImGui.PushID(actor.Id);

        var count = actor.Children.Count;
        var flags = ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.AllowOverlap | ImGuiTreeNodeFlags.FramePadding;
        if (actor.IsSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (count == 0) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        var anyChildSelected = _scrollToSelected && HasSelectedDescendant(actor);
        if (anyChildSelected && count > 0) ImGui.SetNextItemOpen(true);

        var open = ImGui.TreeNodeEx("##Tree", flags);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            SelectedActor = actor;
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ActiveCamera != null && actor.RootComponent is not null)
        {
            var (center, distance) = actor.RootComponent.GetTeleportPosition(ActiveCamera);
            ActiveCamera.TeleportTo(center + ActiveCamera.Forward * distance);
        }

        ImGui.SameLine();
        if (Icons.TryGetValue(actor.Icon, out var icon))
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.Image(icon.GetPointer(), _iconSize);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 2);
        }
        else
        {
            ImGui.Dummy(_iconSize);
        }

        ImGui.SameLine();
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int)EFondIndex.SegoeuiSemiBold]);
        ImGui.PushStyleColor(ImGuiCol.Text, actor.IsVisible ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiCol.TextDisabled));
        ImGui.TextUnformatted(actor.Name);
        ImGui.PopStyleColor();
        ImGui.PopFont();

        DrawActorActionButtons(actor);

        if (open && count > 0 && actor.Children.ToList() is { Count: > 0 } children)
        {
            var anyChildExpanded = false;
            if (clip && !anyChildSelected)
            {
                foreach (var child in children)
                {
                    ImGui.PushID(child.Id);
                    var isOpen = ImGui.GetStateStorage().GetInt(ImGui.GetID("##Tree")) != 0;
                    ImGui.PopID();
                    if (isOpen && child.Children.Count > 0)
                    {
                        anyChildExpanded = true;
                        break;
                    }
                }
            }

            // only use clipper if no child is expanded and not scrolling to selected
            if (clip && !anyChildSelected && !anyChildExpanded)
            {
                unsafe
                {
                    var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                    clipper.Begin(children.Count);
                    while (clipper.Step())
                    {
                        for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            DrawActorTree(children[i]);
                        }
                    }
                    clipper.End();
                    clipper.Destroy();
                }
            }
            else foreach (var child in children)
            {
                DrawActorTree(child);
            }

            ImGui.TreePop();
        }

        if (actor.IsSelected && _scrollToSelected)
        {
            ImGui.SetScrollHereY();
            _scrollToSelected = false;
        }

        ImGui.PopID();
    }

    private void DrawActorActionButtons(Actor actor)
    {
        var actionButtons = new List<(string id, string icon, string tooltip, Action action, bool enabled)>
        {
            ("visibility", actor.IsVisible ? "eye" : "eye_closed", actor.IsVisible ? "Hide" : "Show", () => actor.IsVisible = !actor.IsVisible, true),
            ("delete", "trash", "Delete", () =>
            {
                actor.Parent?.Children.Remove(actor);
                if (actor.IsSelected) SelectedActor = null;
            }, true),
        };

        if (actor is CellActor cell)
        {
            actionButtons.Insert(0, ("download", cell.IsLoaded ? "download_off" : "download", "Load", () => cell.EnqueueLoad(), cell is { CanLoad: true, IsLoaded: false, IsLoading: false }));
        }

        switch (actor.RootComponent)
        {
            case SceneCameraComponent { IsActive: true }:
                actionButtons.RemoveRange(0, 2);
                break;
        }

        if (actionButtons.Count == 0) return;

        var style = ImGui.GetStyle();
        var buttonX = ImGui.GetWindowWidth() - style.FramePadding.X - style.WindowPadding.X;
        buttonX -= ImGui.GetScrollMaxY() > 0 ? style.ScrollbarSize : 0;
        buttonX -= actionButtons.Count * (_actionIconSize.X + style.ItemSpacing.X) - style.ItemSpacing.X / 2;

        ImGui.SameLine();
        ImGui.SetCursorPosX(buttonX);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 0 });
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.3f, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.4f, 0.4f, 0.5f));

        var buttonY = ImGui.GetCursorPosY();
        for (var i = 0; i < actionButtons.Count; i++)
        {
            ImGui.SetCursorPosY(buttonY);

            var (id, iconName, tooltip, action, enabled) = actionButtons[i];
            ImGui.BeginDisabled(!enabled);
            if (Icons.TryGetValue(iconName, out var buttonIcon))
            {
                if (ImGui.ImageButton(id, buttonIcon.GetPointer(), _actionIconSize))
                {
                    action();
                }
            }
            else
            {
                if (ImGui.SmallButton(id[0].ToString().ToUpper()))
                {
                    action();
                }
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }

            if (i < actionButtons.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar();
    }

    private void DrawActorInspector()
    {
        if (SelectedComponent is not { } component)
        {
            component = SelectedActor?.RootComponent;
            if (component is null)
            {
                ImGui.TextUnformatted("No actor or component selected.");
                return;
            }
        }

        if (component.Actor is not { } actor)
        {
            ImGui.TextUnformatted("This component is not assigned to any actor.");
            return;
        }

        actor.DrawInterface();

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

                var selected = c.Id == SelectedComponent?.Id;
                if (ImGui.Selectable(c.Name, selected))
                {
                    SelectedComponent = c;
                }

                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        component.DrawInterface();
    }

    private void DrawCameraControls(SceneCameraComponent camera, Vector2 viewportPos, float margin)
    {
        var controlsPos = new Vector2(viewportPos.X + margin, viewportPos.Y + margin);

        ImGui.SetCursorScreenPos(controlsPos);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f, 4f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.4f, 0.4f, 0.4f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, ImGui.GetColorU32(ImGuiCol.HeaderActive));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 3f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, 12f);

        if (Pairs.Count > 1)
        {
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));

            var buttonSize = new Vector2(16);
            if (ImGui.ImageButton("PreviousCameraButton", Icons["arrow-left"].GetPointer(), buttonSize))
            {
                _selectedCameraIndex = (_selectedCameraIndex - 1 + Pairs.Count) % Pairs.Count;
                ActiveCamera = Pairs[_selectedCameraIndex].Camera;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Previous Camera");

            ImGui.SameLine(0, 2f);
            var cameraName = Pairs[_selectedCameraIndex].Camera.Actor?.Name ?? $"Camera {_selectedCameraIndex}";
            ImGui.Button(cameraName, new Vector2(140, 0));
            ImGui.SameLine(0, 2f);

            if (ImGui.ImageButton("NextCameraButton", Icons["arrow-right"].GetPointer(), buttonSize))
            {
                _selectedCameraIndex = (_selectedCameraIndex + 1) % Pairs.Count;
                ActiveCamera = Pairs[_selectedCameraIndex].Camera;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Next Camera");

            ImGui.PopStyleColor();
            ImGui.EndGroup();

            ImGui.SameLine(0, 12f);
        }

        var toggleSize = new Vector2(32, 0);
        ImGui.BeginGroup();
        {
            var fbo = ShowFramebuffers;
            if (fbo) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Button("FBO", toggleSize)) ShowFramebuffers = !fbo;
            if (fbo) ImGui.PopStyleColor(1);

            ImGui.SameLine(0, 2f);

            var fxaa = camera.bFXAA;
            if (fxaa) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Button("AA", toggleSize)) camera.bFXAA = !fxaa;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Anti-Aliasing: {(fxaa ? "ON" : "OFF")}");
            if (fxaa) ImGui.PopStyleColor(1);

            ImGui.SameLine(0, 2f);

            var ao = camera.bAmbientOcclusion;
            if (ao) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Button("AO", toggleSize)) camera.bAmbientOcclusion = !ao;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Ambient Occlusion: {(ao ? "ON" : "OFF")}");
            if (ao) ImGui.PopStyleColor(1);
        }
        ImGui.EndGroup();

        ImGui.SameLine(0, 12f);

        ImGui.BeginGroup();
        {
            var vertexColors = DebugColorMode == ActorDebugColorMode.VertexColors;
            if (vertexColors) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Button("VC", toggleSize)) DebugColorMode = vertexColors ? ActorDebugColorMode.None : ActorDebugColorMode.VertexColors;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Vertex Colors: {(vertexColors ? "ON" : "OFF")}");
            if (vertexColors) ImGui.PopStyleColor(1);

            ImGui.SameLine(0, 0);

            var primitiveColors = DebugColorMode == ActorDebugColorMode.PerPrimitive;
            if (primitiveColors) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
            if (ImGui.Button("PC", toggleSize)) DebugColorMode = primitiveColors ? ActorDebugColorMode.None : ActorDebugColorMode.PerPrimitive;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Primitive Colors: {(primitiveColors ? "ON" : "OFF")}");
            if (primitiveColors) ImGui.PopStyleColor(1);
        }
        ImGui.EndGroup();

        ImGui.SameLine(0, 12f);

        ImGui.BeginGroup();
        {
            var fov = camera.FieldOfView;
            ImGui.SetNextItemWidth(120);
            if (ImGui.SliderFloat("##FOV", ref fov, 1.0f, 89.0f, "FOV %.0f°", ImGuiSliderFlags.AlwaysClamp))
                camera.FieldOfView = fov;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Field of View: {fov:F1}°");

            ImGui.SameLine(0, 2f);

            var speed = camera.MovementSpeed;
            ImGui.SetNextItemWidth(120);
            if (ImGui.SliderFloat("##Speed", ref speed, 1f, 100f, "Speed %.0f m/s", ImGuiSliderFlags.AlwaysClamp))
                camera.MovementSpeed = speed;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Movement Speed: {speed:F1}");
        }
        ImGui.EndGroup();

        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(4);
    }
}
