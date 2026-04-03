using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using ImGuizmoNET;
using OpenTK.Windowing.Common;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Transforms;
using Editor.Widgets;
using Snooper.Core.Containers;
using Snooper.Extensions;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Skybox;
using Snooper.UI;

namespace Editor.Managers;

public class EditorManager(GameWindow wnd) : InterfaceManager(wnd)
{
    private bool _test = true;
    private OPERATION _gizmoOperation = OPERATION.TRANSLATE;
    private readonly ProfilerWidget _profiler = new();
    private readonly SkeletonOverlayWidget _skeletonOverlay = new();
    private readonly SplineOverlayWidget _splineOverlay = new();
    private readonly ViewportAxisWidget _axisWidget = new();

    public override void Update(float delta)
    {
        base.Update(delta);

        _axisWidget.Update(delta);
    }

    protected override void RenderInterface()
    {
        base.RenderInterface();

        ImGui.ShowDemoWindow();

        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);
        if (ImGui.Begin("\ue209 Scene", ref _test))
        {
            GizmoButton("\uf047", OPERATION.TRANSLATE); ImGui.SameLine();
            GizmoButton("\uf2f1", OPERATION.ROTATE); ImGui.SameLine();
            GizmoButton("\uf424", OPERATION.SCALE);

            var size = ImGui.GetContentRegionAvail();
            size.X -= ImGui.GetScrollX();
            size.Y -= ImGui.GetScrollY();

            MainViewport?.Camera.Resize((int) size.X, (int) size.Y);

            var texture = Pipeline.GetFinalTexture();
            ImGui.Image(texture.GetPointer(), size, Vector2.UnitY, Vector2.UnitX);
            var itemMin = ImGui.GetItemRectMin();
            var drawList = ImGui.GetWindowDrawList();

            ImGuizmo.SetDrawlist(drawList);
            ImGuizmo.SetRect(itemMin.X, itemMin.Y, size.X, size.Y);

            if (MainViewport?.Camera is { } camera)
            {
                if (_axisWidget.Draw(drawList, camera, itemMin with { X = itemMin.X + size.X }))
                {
                    camera.SnapRotationTo(_axisWidget.SnapRotations[_axisWidget.HoveredAxis]);
                }

                var component = SelectedComponent ?? SelectedActor?.RootComponent;
                if (component is SpatialComponent s)
                {
                    var view = camera.ViewMatrix;
                    var proj = camera.ProjectionMatrix;
                    var matrix = s.GizmoMatrix;

                    switch (s)
                    {
                        case SplineMeshComponent { Actor: { } splineActor }:
                        {
                            _splineOverlay.BeginFrame();
                            foreach (var sm in splineActor.Components.OfType<SplineMeshComponent>())
                                _splineOverlay.Feed(sm);
                            _splineOverlay.DrawOverlay(drawList, camera, itemMin, size);
                            var overlayAction = _splineOverlay.EndFrame(drawList, itemMin, size);
                            if (overlayAction is SplineOverlayAction.Changed)
                                _splineOverlay.SelectedSpline?.MarkDirty(DirtyFlags.Spline);

                            if (_splineOverlay.SelectedHandle != -1 && _splineOverlay.SelectedSpline is not null)
                            {
                                var handleMatrix = _splineOverlay.SelectedHandleMatrix;
                                if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, OPERATION.TRANSLATE, MODE.WORLD, ref handleMatrix.M11))
                                {
                                    _splineOverlay.ApplyGizmoMatrix(handleMatrix);
                                    _splineOverlay.SelectedSpline.MarkDirty(DirtyFlags.Spline);
                                }
                            }

                            break;
                        }
                        case MeshComponent mesh when mesh.Descriptor.Sockets.Length > 0 || mesh.Descriptor.Skeleton != null:
                        {
                            _skeletonOverlay.Draw(drawList, mesh, s.WorldMatrix, camera, itemMin, size);

                            var boneIndex = _skeletonOverlay.SelectedBoneIndex;
                            if (boneIndex >= 0 && mesh.Descriptor.Skeleton is { } skeleton)
                            {
                                matrix = skeleton.BoneMatrices[boneIndex] * s.GizmoMatrix;

                                if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, _gizmoOperation, MODE.LOCAL, ref matrix.M11))
                                {
                                    Matrix4x4.Invert(s.GizmoMatrix, out var invGizmo);
                                    skeleton.MoveBone(boneIndex, matrix * invGizmo);
                                    mesh.MarkDirty(DirtyFlags.Animation);
                                }
                            }

                            break;
                        }
                        case DirectionalLightComponent:
                        {
                            if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, OPERATION.ROTATE_X | OPERATION.ROTATE_Y | OPERATION.ROTATE_SCREEN, MODE.LOCAL, ref matrix.M11))
                            {
                                s.ApplyGizmoMatrix(matrix);
                            }
                            break;
                        }
                        case GridComponent or AtmosphericComponent: break;
                        default:
                        {
                            if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, _gizmoOperation, MODE.LOCAL, ref matrix.M11))
                            {
                                s.ApplyGizmoMatrix(matrix);
                            }
                            break;
                        }
                    }
                }
            }

            if (ImGui.IsItemHovered() && !ImGuizmo.IsUsing() && !_skeletonOverlay.IsUsing && !_splineOverlay.IsUsing)
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    Window.CursorState = CursorState.Grabbed;
                }

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    OnViewportLeftClick(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), size);
                }
            }

            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold]);

            const float margin = 7.5f;
            var frameHeight = ImGui.GetFrameHeight();

            var framerate = ImGui.GetIO().Framerate;
            drawList.AddText(
                new Vector2(itemMin.X + margin, itemMin.Y + size.Y - frameHeight),
                ImGui.GetColorU32(ImGuiCol.Text),
                $"FPS: {framerate:0} ({1000.0f / framerate:0.##} ms) ({texture.Width} x {texture.Height} px)"
            );

            const string label = "\uf06a Previewed content may differ from final version saved or used in-game.";
            drawList.AddText(
                new Vector2(itemMin.X + size.X - ImGui.CalcTextSize(label).X - margin, itemMin.Y + size.Y - frameHeight),
                ImGui.GetColorU32(new Vector4(1.00f, 1.00f, 1.00f, 0.50f)),
                label
            );

            ImGui.PopFont();
        }
        ImGui.PopStyleVar(2);
        ImGui.End();

        SceneHierarchyWidget.Draw(RootActor);
        InspectorWidget.Draw(SelectedActor, SelectedComponent);
        JsonViewerWidget.DrawAll();

        if (ImGui.Begin("\uf013 Render World Settings"))
        {
            DrawControls();
        }
        ImGui.End();

        if (ImGui.Begin("\uf187 Content"))
        {

        }
        ImGui.End();

        if (ImGui.Begin("\ue0e4 Timeline"))
        {

        }
        ImGui.End();

        if (ImGui.Begin("\uf120 Log"))
        {

        }
        ImGui.End();

        if (ImGui.Begin("\uf200 Profiler"))
        {
            if (ImGui.BeginTabBar("ProfilerTabs"))
            {
                if (ImGui.BeginTabItem("Overview"))
                {
                    ImGui.Columns(2, "SysInfo", false);
                    ImGui.Text($"API: {Renderer.Name}");
                    ImGui.Text($"GPU: {Renderer.DeviceInfo.Name}");
                    ImGui.NextColumn();
                    ImGui.Text($"OpenGL: {Renderer.Version}");
                    ImGui.Text($"Vendor: {Renderer.DeviceInfo.Vendor}");
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
                    _profiler.DrawMemorySummary(this);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Memory"))
                {
                    _profiler.DrawMemoryTable(this);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Systems"))
                {
                    foreach (var system in Systems.Values)
                    {
                        var isBusy = system.DirtyComponentsCount > 0;
                        if (isBusy)
                        {
                            var timeColor = ImGui.GetColorU32(new Vector4(0.8f, 0.5f, 0.0f, 0.5f + 0.5f * (float) Math.Sin(Time * 5)));
                            ImGui.PushStyleColor(ImGuiCol.Header, timeColor);
                            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, timeColor);
                        }
                        if (ImGui.CollapsingHeader($"{system.Order}. {system.DisplayName}"))
                        {
                            ImGui.Columns(2, $"SysTable{system.Order}", false);
                            {
                                ImGui.TextDisabled("Components");
                                ImGui.TextUnformatted($"{system.ComponentsCount:N0} {system.ComponentType.Name}{(system.ComponentsCount > 1 ? "s" : "")}");
                                ImGui.Spacing();
                                ImGui.TextDisabled("Dirty Components");
                                ImGui.TextUnformatted($"{system.DirtyComponentsCount:N0} {system.ComponentType.Name}{(system.DirtyComponentsCount > 1 ? "s" : "")}");
                                ImGui.Spacing();
                                system.Profiler.PollResults();
                                ImGui.TextDisabled("Primitives");
                                ImGui.TextUnformatted($"{system.Profiler.PrimitivesGenerated:N0}");
                                ImGui.NextColumn();
                                ImGui.TextDisabled("Show Wireframe");
                                ImGui.Checkbox($"##ShowWireframe{system.Order}", ref system.ShowWireframe);
                                ImGui.Spacing();
                                ImGui.TextDisabled("Is Enabled");
                                ImGui.Checkbox($"##Enabled{system.Order}", ref system.IsEnabled);
                            }
                            ImGui.Columns(1);
                            if (system is IMemorySizeProvider provider)
                            {
                                ImGui.Spacing();
                                _profiler.DrawMemorySummary(provider);
                            }
                            if (ImGui.TreeNode($"Performance Metrics##SysMetrics{system.Order}"))
                            {
                                _profiler.DrawPerformanceMetrics(system.Profiler, system.Order.ToString());
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
                        if (isBusy)
                        {
                            ImGui.PopStyleColor(2);
                        }
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }

    private void GizmoButton(string icon, OPERATION operation)
    {
        var isActive = _gizmoOperation == operation;
        if (isActive)
        {
            var col = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            ImGui.PushStyleColor(ImGuiCol.Button, col);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, col);
        }

        if (ImGui.Button(icon))
            _gizmoOperation = operation;

        if (isActive)
            ImGui.PopStyleColor(2);
    }

    protected override void OnSelectionChanged(Actor? actor, ActorComponent? component)
    {
        _skeletonOverlay.Reset();
        _splineOverlay.Reset();
    }
}
