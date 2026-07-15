using System.Numerics;
using CUE4Parse.FileProvider;
using Editor.Modals;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Editor.Widgets;
using Snooper.Core.Containers;
using Snooper.Extensions;
using Snooper.UI;

namespace Editor.Managers;

public class EditorManager(GameWindow wnd, IFileProvider fileProvider) : InterfaceManager(wnd, fileProvider)
{
    private readonly InspectorWidget _inspector = new();
    private readonly SceneHierarchyWidget _sceneHierarchy = new();
    private readonly ViewportWidget _viewport = new();
    private readonly ProfilerWidget _profiler = new();

    internal readonly ViewportAxisWidget _viewportAxis = new();
    internal readonly ProfilerOverlayWidget _profilerOverlay = new();
    internal readonly JsonViewerWidget _jsonViewer = new();
    internal readonly SkeletonOverlayWidget _skeletonOverlay = new();
    internal readonly SplineOverlayWidget _splineOverlay = new();

    public override void Update(float delta)
    {
        base.Update(delta);

        _viewportAxis.Update(delta);
    }

    protected override void RenderInterface()
    {
        base.RenderInterface();

        ImGui.ShowDemoWindow();

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
                                var capacity = system.Capacity >= 0 ? $"/{system.Capacity:N0}" : "";
                                ImGui.TextDisabled("Components");
                                ImGui.TextUnformatted($"{system.ComponentsCount:N0}{capacity} {system.ComponentType.Name}{(system.ComponentsCount > 1 ? "s" : "")}");
                                ImGui.Spacing();
                                ImGui.TextDisabled("Dirty Components");
                                ImGui.TextUnformatted($"{system.DirtyComponentsCount:N0}{capacity} {system.ComponentType.Name}{(system.DirtyComponentsCount > 1 ? "s" : "")}");
                                ImGui.Spacing();
                                ImGui.TextDisabled("Max Binding Used");
                                ImGui.TextUnformatted($"{system.MaxBindingUsed?.ToString() ?? "N/A"}");
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

        _inspector.Draw(SelectedActor, SelectedComponent);
        _sceneHierarchy.Draw(RootActor);
        _viewport.Draw(MainViewport);

        _jsonViewer.DrawAll();

        ExportModal.Instance.Draw();
    }

    protected override void OnSelectionChanged(Actor? actor, ActorComponent? component)
    {
        _skeletonOverlay.Reset();
        _splineOverlay.Reset();
    }
}
