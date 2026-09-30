using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using ImGuizmoNET;
using OpenTK.Windowing.Common;
using Snooper;
using Snooper.Core;
using Snooper.Core.Hardware;
using Snooper.Core.Systems;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Editor.Widgets;

public class ViewportWidget : PanelWidget
{
    public override string PanelTitle => Settings.ViewportWindow;
    public override PanelGroup Group => PanelGroup.Editor;
    public override bool CanClose => false;
    public override bool IsOpen { get => true; set { } }

    private static float Padding => ImGui.GetFrameHeight() * 0.35f;

    private readonly ViewportGrid _grid = new();
    private OPERATION _gizmoOperation = OPERATION.TRANSLATE;
    private bool _edit;

    protected override void PushWindowStyle() => ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    protected override void PopWindowStyle() => ImGui.PopStyleVar();

    protected override void DrawContents(EditorManager editor)
    {
        if (editor.MainViewport is not { } viewport)
        {
            ImGui.TextDisabled("No viewport.");
            return;
        }

        if (viewport.Camera.Actor?.ActorManager is not InterfaceManager manager)
        {
            ImGui.TextDisabled("No camera.");
            return;
        }

        var contentPos = ImGui.GetCursorScreenPos();
        var contentSize = ImGui.GetContentRegionAvail();
        contentSize.X -= ImGui.GetScrollX();
        contentSize.Y -= ImGui.GetScrollY();

        viewport.Camera.Resize((int) contentSize.X, (int) contentSize.Y);
        ImGui.Image(manager.Pipeline.GetFinalTexture().GetPointer(), contentSize, Vector2.UnitY, Vector2.UnitX);
        var imageHovered = ImGui.IsItemHovered();

        var itemMin = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();
        ImGuizmo.SetDrawlist(drawList);
        ImGuizmo.SetRect(itemMin.X, itemMin.Y, contentSize.X, contentSize.Y);

        _grid.Reset(contentPos + contentSize with { Y = 0f });
        var context = new ViewportContext(viewport.Camera, drawList, contentPos, contentSize, _gizmoOperation, MODE.LOCAL, _grid);

        bool captured;
        using (Profiler.Cpu("Axis")) captured = editor._viewportAxis.Draw(context);

        var component = manager.SelectedComponent ?? manager.SelectedActor?.RootComponent;
        using (Profiler.Cpu("Gizmos"))
        {
            captured |= component switch
            {
                SplineMeshComponent spline when _edit => editor._splineOverlay.Draw(context, spline),
                MeshComponent mesh when _edit => editor._skeletonOverlay.Draw(context, mesh),
                DirectionalLightComponent light => editor._sunOverlay.Draw(context, light),
                SpatialComponent spatial when editor.EditTransforms => DrawGizmo(context, spatial),
                _ => false
            };
        }

        using (Profiler.Cpu("Toolbar")) DrawToolbar(editor, contentPos);
        using (Profiler.Cpu("Cards")) DrawCards(editor, contentPos, contentSize);

        using (Profiler.Cpu("Footer")) DrawFooterOverlay(contentPos, contentSize);
        using (Profiler.Cpu("Notifications")) editor._notificationOverlay.Draw(drawList, contentPos, contentSize);

        if (imageHovered && !ImGui.IsAnyItemActive() && !ImGuizmo.IsUsing() && !captured)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                manager.Window.CursorState = CursorState.Grabbed;
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                manager.OnViewportLeftClick(ImGui.GetMousePos(), contentPos, contentSize);
            }
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && component is SpatialComponent spatial)
            {
                spatial.TeleportTo();
                Notifications.Push("camera.focus", Settings.FocusIcon, $"Focused {spatial.Name}");
            }
        }

        if (manager.Window.CursorState == CursorState.Grabbed && viewport.Camera is { ViewType: CameraType.Orbital } orbitalCamera)
        {
            DrawOrbitCircle(orbitalCamera, component, contentPos, contentSize);
        }
    }

    private void DrawToolbar(EditorManager editor, Vector2 contentPos)
    {
        var style = ImGui.GetStyle();
        ImGui.SetCursorScreenPos(contentPos + new Vector2(Padding, Padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X * 0.25f));

        if (Button(Settings.PenToSquareIcon,_edit, "Edit")) _edit = !_edit;

        Separator();
        var lights = editor.GetSystem<ClusteredLightSystem>();
        if (Button(Settings.LightbulbIcon,lights?.UseSceneLights == true, "Lighting", lights != null)) lights!.UseSceneLights = !lights.UseSceneLights;
        ImGui.SameLine();
        if (Button(Settings.CircleHalfStrokeIcon,editor.Pipeline.Shadows, "Shadows")) editor.Pipeline.Shadows = !editor.Pipeline.Shadows;
        ImGui.SameLine();
        if (Button(Settings.DrawPolygonIcon, editor.Wireframe.Enabled, "Wireframe", DeviceInfo.HasFragmentBarycentric)) editor.Wireframe.Enabled = !editor.Wireframe.Enabled;

        Separator();
        SystemButton<BillboardSystem>(Settings.ChalkboardIcon,"Billboards");
        ImGui.SameLine();
        SystemButton<TextRenderSystem>(Settings.FontIcon,"Text");
        ImGui.SameLine();
        SystemButton<DebugSystem>(Settings.BugIcon,"Debug Shapes");

        Separator();
        CardButton(Settings.ChartLineIcon, editor._profilerOverlay);
        ImGui.SameLine();
        CardButton(Settings.MicrochipIcon, editor._hardwareOverlay);

        if (_edit || editor.EditTransforms)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentPos.X + Padding, ImGui.GetCursorScreenPos().Y));
            if (Button(Settings.ArrowsUpDownLeftRightIcon,_gizmoOperation == OPERATION.TRANSLATE, "Translate")) _gizmoOperation = OPERATION.TRANSLATE;
            ImGui.SameLine();
            if (Button(Settings.RotateIcon,_gizmoOperation == OPERATION.ROTATE, "Rotate")) _gizmoOperation = OPERATION.ROTATE;
            ImGui.SameLine();
            if (Button(Settings.MinimizeIcon,_gizmoOperation == OPERATION.SCALE, "Scale")) _gizmoOperation = OPERATION.SCALE;
        }

        ImGui.PopStyleVar();

        void Separator()
        {
            ImGui.SameLine();
            EditorUI.VerticalSeparator();
            ImGui.SameLine();
        }

        void SystemButton<T>(string icon, string tooltip) where T : ActorSystem
        {
            var system = editor.GetSystem<T>();
            if (Button(icon, system?.IsEnabled == true, tooltip, system != null)) system!.IsEnabled = !system.IsEnabled;
        }

        void CardButton(string icon, IViewportCard card)
        {
            if (Button(icon, card.IsOpen, card.Title)) card.IsOpen = !card.IsOpen;
        }
    }

    private void DrawCards(EditorManager editor, Vector2 contentPos, Vector2 contentSize)
    {
        var any = false;
        foreach (var card in editor.Cards) any |= card.IsOpen;
        if (!any) return;

        var width = MathF.Round(ImGui.GetFrameHeight() * 15f);
        var position = new Vector2(MathF.Round(contentPos.X + Padding), MathF.Round(ImGui.GetCursorScreenPos().Y + Padding));
        var bottom = contentPos.Y + contentSize.Y - ImGui.GetTextLineHeight() - Padding * 2f;
        if (bottom - position.Y < ImGui.GetFrameHeight() * 4f) return;

        ImGui.SetCursorScreenPos(position);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, MathF.Floor(bottom - position.Y)));
        var visible = ImGui.BeginChild("##Cards", new Vector2(width, 0f), ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AutoResizeY);

        if (visible)
        {
            foreach (var card in editor.Cards)
            {
                if (card.IsOpen && BeginCard(card.Title))
                {
                    card.Draw(editor);
                    ImGui.EndChild();
                }
            }
        }
        ImGui.EndChild();

        bool BeginCard(string title)
        {
            if (!ImGui.TreeNodeEx(title, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen)) return false;

            if (ImGui.BeginChild($"##{title}", Vector2.Zero, ImGuiChildFlags.FrameStyle | ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AutoResizeY)) return true;

            ImGui.EndChild();
            return false;
        }
    }

    private bool DrawGizmo(in ViewportContext viewport, SpatialComponent spatial)
    {
        var matrix = spatial.GizmoMatrix;
        if (viewport.Manipulate(ref matrix))
        {
            spatial.ApplyGizmoMatrix(matrix);
        }

        return false;
    }

    private void DrawFooterOverlay(Vector2 contentPos, Vector2 contentSize)
    {
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold]);

        var io = ImGui.GetIO();
        var text = $"FPS: {io.Framerate:F1} ({io.DeltaTime * 1000f:F2} ms)";
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorScreenPos(contentPos + new Vector2(Padding, contentSize.Y - Padding - size.Y));
        ImGui.TextUnformatted(text);

        text = "\uf06a Previewed content may differ from final version saved or used in-game.";
        size = ImGui.CalcTextSize(text);
        ImGui.SetCursorScreenPos(contentPos + new Vector2(contentSize.X - Padding - size.X, contentSize.Y - Padding - size.Y));
        ImGui.TextUnformatted(text);

        ImGui.PopFont();
    }

    private void DrawOrbitCircle(InteractiveCameraComponent camera, ActorComponent? component, Vector2 contentPos, Vector2 contentSize)
    {
        var orbitCenter = camera.GetLocalTransform().Position - camera.Forward * camera.OrbitDistance;
        var circleY = component is SpatialComponent spatial ? spatial.GizmoMatrix.Translation.Y : 0f;
        var viewProj = camera.ViewMatrix * camera.ProjectionMatrix;

        var drawList = ImGui.GetWindowDrawList();
        var col = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f));
        var radius = MathF.Max(camera.OrbitDistance * 0.4f, 0.15f);

        Vector2? Project(Vector3 wp)
        {
            var clip = Vector4.Transform(new Vector4(wp, 1f), viewProj);
            if (clip.W <= 0f) return null;

            return new Vector2(contentPos.X + (clip.X / clip.W * 0.5f + 0.5f) * contentSize.X, contentPos.Y + (0.5f - clip.Y / clip.W * 0.5f) * contentSize.Y);
        }

        const int segments = 64;
        for (var i = 0; i < segments; i++)
        {
            var a0 = i * (MathF.PI * 2f / segments);
            var a1 = (i + 1) * (MathF.PI * 2f / segments);
            var p0 = new Vector3(orbitCenter.X + MathF.Cos(a0) * radius, circleY, orbitCenter.Z + MathF.Sin(a0) * radius);
            var p1 = new Vector3(orbitCenter.X + MathF.Cos(a1) * radius, circleY, orbitCenter.Z + MathF.Sin(a1) * radius);

            if (Project(p0) is { } sp0 && Project(p1) is { } sp1)
            {
                drawList.AddLine(sp0, sp1, col, 1.0f);
            }
        }
    }

    private bool Button(string icon, bool active, string tooltip, bool enabled = true)
    {
        var color = ImGui.GetColorU32(ImGuiCol.ButtonActive);

        ImGui.BeginDisabled(!enabled);
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, color);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        }

        var clicked = ImGui.Button(icon);

        if (active) ImGui.PopStyleColor(2);
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) EditorUI.Tooltip(tooltip);
        return clicked;
    }
}
