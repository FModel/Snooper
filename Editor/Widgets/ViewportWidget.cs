using System.Numerics;
using Editor.Managers;
using ImGuiNET;
using ImGuizmoNET;
using OpenTK.Windowing.Common;
using Snooper;
using Snooper.Core;
using Snooper.Core.Hardware;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Managers;

namespace Editor.Widgets;

public class ViewportWidget
{
    private const string Title = "Viewport";
    private const float Padding = 7.5f;

    private const string SelectIcon    = "\uf245"; // mouse-pointer
    private const string TranslateIcon = "\uf047"; // arrows-alt
    private const string RotateIcon    = "\uf2f1"; // sync-alt
    private const string ScaleIcon     = "\uf424"; // compress-arrows-alt
    private const string WorldIcon     = "\uf0ac"; // globe
    private const string LocalIcon     = "\uf5a0"; // object-group
    private const string FreeIcon      = "\uf48b"; // street-view
    private const string OrbitalIcon   = "\uf140"; // bullseye
    private const string ProfilerIcon  = "\uf201"; // chart-line
    private const string HardwareIcon  = "\uf2db"; // microchip

    private OPERATION _gizmoOperation = OPERATION.TRANSLATE;
    private bool _localSpace = true;
    private bool _selectMode;

    public void Draw(Viewport? viewport)
    {
        if (viewport == null) return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var visible = ImGui.Begin(Title);
        ImGui.PopStyleVar();

        if (!visible)
        {
            ImGui.End();
            return;
        }

        if (viewport.Camera.Actor?.ActorManager is not InterfaceManager manager)
        {
            ImGui.TextDisabled("No camera.");
            ImGui.End();
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

        var component = manager.SelectedComponent ?? manager.SelectedActor?.RootComponent;
        DrawComponentControlsOverlay(viewport.Camera, component, contentPos, contentSize);

        var editorManager = manager as EditorManager;
        var bandHeight = editorManager?._hardwareOverlay.Draw(drawList, contentPos, contentSize, manager) ?? 0f;

        DrawToolbar(viewport.Camera, contentPos);
        var clicked = DrawAxisOverlay(viewport.Camera, contentPos, contentSize);
        DrawStatsOverlay(contentPos, contentSize, bandHeight);

        editorManager?._profilerOverlay.Draw(drawList, contentPos, contentSize, bandHeight);
        editorManager?._notificationOverlay.Draw(drawList, contentPos, contentSize, bandHeight);

        if (imageHovered && !ImGui.IsAnyItemActive() && !ImGuizmo.IsUsing() && !clicked)
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

        ImGui.End();
    }

    private void DrawToolbar(InteractiveCameraComponent camera, Vector2 contentPos)
    {
        var style = ImGui.GetStyle();
        ImGui.SetCursorScreenPos(contentPos + new Vector2(Padding, Padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing with { X = 2f });

        ToggleButton(SelectIcon, ref _selectMode, "Select Mode");

        ImGui.SameLine();
        VerticalSeparator(style.FramePadding.Y);
        ImGui.SameLine();

        ImGui.BeginDisabled(_selectMode);
        GizmoButton(TranslateIcon, OPERATION.TRANSLATE, "Translate"); ImGui.SameLine();
        GizmoButton(RotateIcon, OPERATION.ROTATE, "Rotate"); ImGui.SameLine();
        GizmoButton(ScaleIcon, OPERATION.SCALE, "Scale"); ImGui.SameLine();
        VerticalSeparator(style.FramePadding.Y); ImGui.SameLine();
        ToggleButton(_localSpace ? LocalIcon : WorldIcon, ref _localSpace, _localSpace ? "Local Space" : "World Space");
        ImGui.EndDisabled();

        ImGui.SameLine();
        VerticalSeparator(style.FramePadding.Y);
        ImGui.SameLine();

        var isOrbital = camera.ViewType == CameraType.Orbital;
        if (ToggleButton(isOrbital ? OrbitalIcon : FreeIcon, ref isOrbital, isOrbital ? "Orbital Camera" : "Free Camera"))
        {
            camera.ViewType = isOrbital ? CameraType.Orbital : CameraType.Free;
        }

        ImGui.SameLine();
        VerticalSeparator(style.FramePadding.Y);
        ImGui.SameLine();
        ToggleButton(ProfilerIcon, ref Profiler.Enabled, "Profiler");

        ImGui.SameLine();
        ToggleButton(HardwareIcon, ref RendererInfo.TrackMemory, "Hardware");

        ImGui.PopStyleVar();
    }

    private bool DrawAxisOverlay(InteractiveCameraComponent camera, Vector2 contentPos, Vector2 contentSize)
    {
        if (camera.Actor?.ActorManager is not EditorManager manager)
            return false;

        var clicked = manager._viewportAxis.Draw(camera, contentPos + contentSize with { Y = 0f });
        if (clicked) camera.SnapRotationTo(manager._viewportAxis.SnapRotations[manager._viewportAxis.HoveredAxis]);

        return clicked;
    }

    private void DrawStatsOverlay(Vector2 contentPos, Vector2 contentSize, float bottomClearance)
    {
        ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[(int) EFondIndex.SegoeuiSemiBold]);

        var bottom = contentSize.Y - bottomClearance;

        var io = ImGui.GetIO();
        var text = $"FPS: {io.Framerate:F1} ({io.DeltaTime * 1000f:F2} ms)";
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorScreenPos(contentPos + new Vector2(Padding, bottom - Padding - size.Y));
        ImGui.TextUnformatted(text);

        if (Profiler.Enabled)
        {
            var primitives = $"{Profiler.TotalPrimitives:N0} primitives";
            var primitivesSize = ImGui.CalcTextSize(primitives);
            ImGui.SetCursorScreenPos(contentPos + new Vector2(Padding, bottom - Padding - size.Y - primitivesSize.Y));
            ImGui.TextUnformatted(primitives);
        }

        text = "\uf06a Previewed content may differ from final version saved or used in-game.";
        size = ImGui.CalcTextSize(text);
        ImGui.SetCursorScreenPos(contentPos + new Vector2(contentSize.X - Padding - size.X, bottom - Padding - size.Y));
        ImGui.TextUnformatted(text);

        ImGui.PopFont();
    }

    private void DrawComponentControlsOverlay(CameraComponent camera, ActorComponent? component, Vector2 contentPos, Vector2 contentSize)
    {
        var view = camera.ViewMatrix;
        var proj = camera.ProjectionMatrix;

        switch (component)
        {
            case SplineMeshComponent { Actor: { ActorManager: EditorManager manager } splineActor } when _selectMode:
            {
                manager._splineOverlay.BeginFrame();
                foreach (var sm in splineActor.Components.OfType<SplineMeshComponent>())
                    manager._splineOverlay.Feed(sm);

                var drawList = ImGui.GetWindowDrawList();
                manager._splineOverlay.DrawOverlay(drawList, camera, contentPos, contentSize);
                var overlayAction = manager._splineOverlay.EndFrame(drawList, contentPos, contentSize);
                if (overlayAction is SplineOverlayAction.Changed)
                    manager._splineOverlay.SelectedSpline?.MarkDirty(DirtyFlags.Spline);

                if (manager._splineOverlay.SelectedHandle != -1 && manager._splineOverlay.SelectedSpline is not null)
                {
                    var handleMatrix = manager._splineOverlay.SelectedHandleMatrix;
                    if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, OPERATION.TRANSLATE, MODE.WORLD, ref handleMatrix.M11))
                    {
                        manager._splineOverlay.ApplyGizmoMatrix(handleMatrix);
                        manager._splineOverlay.SelectedSpline.MarkDirty(DirtyFlags.Spline);
                    }
                }
                break;
            }
            case MeshComponent { Actor.ActorManager: EditorManager manager } mesh when _selectMode:
            {
                var drawList = ImGui.GetWindowDrawList();
                manager._skeletonOverlay.Draw(drawList, mesh, mesh.WorldMatrix, camera, contentPos, contentSize);

                var boneIndex = manager._skeletonOverlay.SelectedBoneIndex;
                if (boneIndex >= 0 && mesh.Descriptor.Skeleton is { } skeleton)
                {
                    var matrix = skeleton.BoneMatrices[boneIndex] * mesh.GizmoMatrix;

                    if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, _gizmoOperation, MODE.LOCAL, ref matrix.M11))
                    {
                        Matrix4x4.Invert(mesh.GizmoMatrix, out var invGizmo);
                        skeleton.MoveBone(boneIndex, matrix * invGizmo);
                        mesh.MarkDirty(DirtyFlags.Animation);
                    }
                }
                break;
            }
            case DirectionalLightComponent light:
            {
                var matrix = light.GizmoMatrix;
                if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, OPERATION.ROTATE_X | OPERATION.ROTATE_Y | OPERATION.ROTATE_SCREEN | OPERATION.TRANSLATE_Z, MODE.LOCAL, ref matrix.M11))
                {
                    light.ApplyGizmoMatrix(matrix);
                }
                break;
            }
            case SpatialComponent spatial when !_selectMode:
            {
                var matrix = spatial.GizmoMatrix;
                if (ImGuizmo.Manipulate(ref view.M11, ref proj.M11, _gizmoOperation, _localSpace ? MODE.LOCAL : MODE.WORLD, ref matrix.M11))
                {
                    spatial.ApplyGizmoMatrix(matrix);
                }
                break;
            }
        }
    }

    private void DrawOrbitCircle(InteractiveCameraComponent camera, ActorComponent? component, Vector2 contentPos, Vector2 contentSize)
    {
        var orbitCenter = camera.LocalTransform.Position - camera.Forward * camera.OrbitDistance;
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

    private bool GizmoButton(string icon, OPERATION op, string tooltip)
    {
        var active = !_selectMode && _gizmoOperation == op;
        if (active) PushActiveColor();
        var clicked = ImGui.Button(icon);
        if (clicked) _gizmoOperation = op;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        if (active) ImGui.PopStyleColor(2);
        return clicked;
    }

    private bool ToggleButton(string icon, ref bool value, string tooltip)
    {
        var wasOn = value;
        if (wasOn) PushActiveColor();
        var clicked = ImGui.Button(icon);
        if (clicked) value = !value;
        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        if (wasOn) ImGui.PopStyleColor(2);
        return clicked;
    }

    private void PushActiveColor()
    {
        var col = ImGui.GetColorU32(ImGuiCol.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Button, col);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, col);
    }

    private void VerticalSeparator(float paddingY)
    {
        var size = new Vector2(1, ImGui.GetFrameHeight());
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.GetColorU32(ImGuiCol.Separator);
        ImGui.GetWindowDrawList().AddLine(pos with { Y = pos.Y + paddingY }, pos with { Y = pos.Y + size.Y - paddingY }, col, size.X);
        ImGui.Dummy(size);
    }
}
