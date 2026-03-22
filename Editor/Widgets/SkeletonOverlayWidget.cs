using System.Numerics;
using ImGuiNET;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

/// <summary>
/// Draws a skeleton overlay directly on the viewport draw list.
/// Bones are projected from model space to screen space and rendered
/// as clickable dots connected by lines.
/// Left-click  ? select bone for gizmo manipulation.
/// Right-click ? context menu to reset bone(s) to bind pose.
/// </summary>
public class SkeletonOverlayWidget
{
    // -- colours --------------------------------------------------------------
    private const uint ColBone     = 0xFF_C8_C8_C8; // light grey  lines
    private const uint ColDot      = 0xFF_FF_FF_FF; // white       dots
    private const uint ColSelected = 0xFF_30_A0_FF; // blue        selected
    private const uint ColHovered  = 0xFF_FF_C0_30; // amber       hovered
    private const uint ColDotBg    = 0x88_00_00_00; // semi-black  dot outline

    private const float DotRadius = 5.0f;
    private const float HitRadius = 9.0f;
    private const float LineThick = 1.2f;

    private const uint ColSocket   = 0xFF_80_FF_80; // green for sockets

    public int  SelectedBoneIndex { get; private set; } = -1;
    public bool IsUsing           { get; private set; }

    private int     _hoveredBoneIndex  = -1;
    private int     _contextBoneIndex  = -1; // bone targeted by the open context menu
    private Vector2[] _screenPositions = [];
    private bool[]    _visible         = [];

    /// <summary>
    /// Call once per frame inside the Scene window after the Image call.
    /// Returns what action occurred this frame (if any).
    /// </summary>
    public void Draw(ImDrawListPtr drawList, MeshComponent mesh, Matrix4x4 meshWorldMatrix, IViewProjectionProvider camera, Vector2 viewportMin, Vector2 viewportSize)
    {
        var vp = camera.ViewMatrix * camera.ProjectionMatrix;
        var mousePos = ImGui.GetMousePos();
        var bestDist = HitRadius * HitRadius;

        if (mesh.Descriptor.Skeleton is { } skeleton)
        {
            var count = skeleton.BoneCount;
            if (_screenPositions.Length != count)
            {
                _screenPositions = new Vector2[count];
                _visible         = new bool[count];
            }
            for (var i = 0; i < count; i++)
            {
                var worldPos = Vector3.Transform(skeleton.BoneMatrices[i].Translation, meshWorldMatrix);
                _visible[i] = TryProject(worldPos, vp, viewportMin, viewportSize, out _screenPositions[i]);
            }

            // -- Lines ------------------------------------------------------------
            for (var i = 1; i < count; i++)
            {
                var parent = skeleton.GetBoneParentIndex(i);
                if (parent < 0 || !_visible[i] || !_visible[parent]) continue;
                drawList.AddLine(_screenPositions[parent], _screenPositions[i], ColBone, LineThick);
            }

            // -- Hit-test ---------------------------------------------------------
            _hoveredBoneIndex = -1;
            for (var i = 0; i < count; i++)
            {
                if (!_visible[i]) continue;
                var dx = mousePos.X - _screenPositions[i].X;
                var dy = mousePos.Y - _screenPositions[i].Y;
                if (dx * dx + dy * dy < bestDist)
                {
                    bestDist          = dx * dx + dy * dy;
                    _hoveredBoneIndex = i;
                }
            }

            // -- Dots -------------------------------------------------------------
            for (var i = 0; i < count; i++)
            {
                if (!_visible[i]) continue;
                var col = i == SelectedBoneIndex ? ColSelected
                        : i == _hoveredBoneIndex  ? ColHovered
                        :                           ColDot;
                drawList.AddCircleFilled(_screenPositions[i], DotRadius + 1.5f, ColDotBg);
                drawList.AddCircleFilled(_screenPositions[i], DotRadius, col);
            }

            if (_hoveredBoneIndex >= 0)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"[{_hoveredBoneIndex}] {skeleton.GetBoneName(_hoveredBoneIndex)}");
                ImGui.EndTooltip();
            }

            if (SelectedBoneIndex >= 0 && _visible[SelectedBoneIndex])
            {
                var labelPos = _screenPositions[SelectedBoneIndex] + new Vector2(DotRadius + 4, -8);
                drawList.AddText(labelPos, ColSelected,
                    $"[{SelectedBoneIndex}] {skeleton.GetBoneName(SelectedBoneIndex)}");
            }

            if (ImGui.BeginPopup("##BoneCtx"))
            {
                var boneName = _contextBoneIndex >= 0 ? skeleton.GetBoneName(_contextBoneIndex) : "?";
                ImGui.TextDisabled($"[{_contextBoneIndex}] {boneName}");
                ImGui.Separator();

                if (ImGui.MenuItem("\uf0e2  Reset bone"))
                {
                    skeleton.ResetBone(_contextBoneIndex);
                    mesh.MarkDirty(DirtyFlags.Animation);
                    // If the reset bone was selected, keep it selected so gizmo updates
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("\uf0e2  Reset all bones"))
                {
                    skeleton.ResetAllBones();
                    mesh.MarkDirty(DirtyFlags.Animation);
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        // -- Sockets -----------------------------------------------------------
        var sockets = mesh.Descriptor.Sockets;
        int hoveredSocketIndex = -1;
        string? hoveredSocketName = null;
        for (int i = 0; i < sockets.Length; i++)
        {
            var socket = sockets[i];
            if (socket == null) continue;
            var socketModel = mesh.Descriptor.GetSocketModelMatrix(socket.Name);
            var socketWorld = socketModel * meshWorldMatrix;
            var socketPos = Vector3.Transform(Vector3.Zero, socketWorld);
            if (!TryProject(socketPos, vp, viewportMin, viewportSize, out var screenPos)) continue;
            drawList.AddCircleFilled(screenPos, DotRadius + 1.5f, ColDotBg);
            drawList.AddCircleFilled(screenPos, DotRadius, ColSocket);

            // Socket hit test
            var dx = mousePos.X - screenPos.X;
            var dy = mousePos.Y - screenPos.Y;
            if (dx * dx + dy * dy < bestDist)
            {
                hoveredSocketIndex = i;
                hoveredSocketName = socket.Name;
            }
        }

        // -- Tooltip -----------------------------------------------------------
        if (hoveredSocketIndex >= 0)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted($"[Socket] {hoveredSocketName}");
            ImGui.EndTooltip();
        }

        // -- Input -------------------------------------------------------------
        var mouseInViewport = ImGui.IsMouseHoveringRect(viewportMin, viewportMin + viewportSize, false);
        if (mouseInViewport && _hoveredBoneIndex >= 0)
        {
            // Left-click ? select
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                SelectedBoneIndex = _hoveredBoneIndex;
            }

            // Right-click ? open context menu for this bone
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                _contextBoneIndex = _hoveredBoneIndex;
                ImGui.OpenPopup("##BoneCtx");
            }
        }

        IsUsing = _hoveredBoneIndex >= 0 || ImGui.IsPopupOpen("##BoneCtx");
    }

    public void Reset() => SelectedBoneIndex = -1;

    private bool TryProject(Vector3 worldPos, Matrix4x4 viewProj, Vector2 viewportMin, Vector2 viewportSize, out Vector2 screenPos)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1.0f), viewProj);
        if (clip.W <= 0.0f)
        {
            screenPos = Vector2.Zero;
            return false;
        }

        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        if (ndc.X < -1.1f || ndc.X > 1.1f || ndc.Y < -1.1f || ndc.Y > 1.1f || ndc.Z < 0 || ndc.Z > 1)
        {
            screenPos = Vector2.Zero;
            return false;
        }

        screenPos = new Vector2(viewportMin.X + (ndc.X * 0.5f + 0.5f) * viewportSize.X, viewportMin.Y + (1.0f - (ndc.Y * 0.5f + 0.5f)) * viewportSize.Y);
        return true;
    }
}
