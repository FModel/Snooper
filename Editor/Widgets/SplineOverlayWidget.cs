using System.Numerics;
using ImGuiNET;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Editor.Widgets;

public enum SplineOverlayAction { None, Changed }

public class SplineOverlayWidget
{
    // ── handle kinds ──────────────────────────────────────────────────────────
    // Knot  = a position on the chain (shared between 2 adjacent components)
    // Tangent = tangent tip, only shown when the parent knot is selected
    private enum HandleKind { Knot, Tangent }

    // Which end of a component a handle refers to
    private enum End { Start, End }

    // ── colours (ABGR) ────────────────────────────────────────────────────────
    private const uint ColCurve      = 0xFF_C8_C8_C8; // grey curve
    private const uint ColArm        = 0xFF_C8_C8_C8; // tangent arm
    private const uint ColKnot       = 0xFF_FF_FF_FF; // white knot
    private const uint ColTangent    = 0xFF_FF_C0_30; // amber tangent
    private const uint ColSelected   = 0xFF_30_A0_FF; // blue selected
    private const uint ColHovered    = 0xFF_FF_70_20; // orange hovered
    private const uint ColDotBg      = 0x99_00_00_00; // shadow

    // ── sizes ─────────────────────────────────────────────────────────────────
    private const float KnotRadius   = 7.0f;
    private const float TangRadius   = 5.0f;
    private const float HitKnot      = 11.0f;
    private const float HitTang      = 8.0f;
    private const float CurveThick   = 1.8f;
    private const float ArmThick     = 1.0f;
    private const int   CurveSegs    = 24;

    // ── handle record ─────────────────────────────────────────────────────────
    private readonly record struct Handle(
        HandleKind          Kind,
        SplineMeshComponent Spline,   // primary component
        End                 End,      // which end of primary
        SplineMeshComponent? Neighbor, // only for shared knots
        End                 NeighborEnd,
        Vector2             Screen);

    // ── frame state ───────────────────────────────────────────────────────────
    private readonly List<Handle>     _handles    = new(64);
    private readonly List<SplineMeshComponent> _chain = new(16);

    public  bool              IsUsing              { get; private set; }
    public  Matrix4x4         SelectedHandleMatrix { get; private set; } = Matrix4x4.Identity;
    public  SplineMeshComponent? SelectedSpline    { get; private set; }

    // selected handle identity
    private SplineMeshComponent? _selSpline;
    private End                  _selEnd;
    private HandleKind           _selKind = HandleKind.Knot;
    private bool                 _hasSelection;

    private SplineMeshComponent? _hovSpline;
    private End                  _hovEnd;
    private HandleKind           _hovKind;
    private bool                 _hasHover;

    private SplineMeshComponent? _ctxSpline;
    private End                  _ctxEnd;
    private HandleKind           _ctxKind;

    // ── originals ─────────────────────────────────────────────────────────────
    private readonly Dictionary<SplineMeshComponent, SplineMeshParams> _originals = new();

    // Public alias used by EditorManager
    public int SelectedHandle => _hasSelection ? (int)_selKind : -1;

    // ─────────────────────────────────────────────────────────────────────────
    // Frame API
    // ─────────────────────────────────────────────────────────────────────────

    public void BeginFrame()
    {
        IsUsing = false;
        _handles.Clear();
        _chain.Clear();
        _hasHover = false;
    }

    /// <summary>Accumulate one spline component. Call for every SplineMeshComponent in the actor.</summary>
    public void Feed(SplineMeshComponent spline)
    {
        if (!_originals.ContainsKey(spline))
            _originals[spline] = spline.SplineParams;
        _chain.Add(spline);
    }

    /// <summary>
    /// After all Feed() calls: sort chain, project, draw curves + tangent arms,
    /// accumulate handles. Call EndFrame() afterwards for hit-test + dots + input.
    /// </summary>
    public void DrawOverlay(
        ImDrawListPtr           drawList,
        IViewProjectionProvider camera,
        Vector2                 viewportMin,
        Vector2                 viewportSize)
    {
        if (_chain.Count == 0) return;

        SortChain();

        var vp = camera.ViewMatrix * camera.ProjectionMatrix;

        // Update gizmo matrix for selected handle
        if (_hasSelection && _selSpline is not null)
        {
            var wm  = _selSpline.WorldMatrix;
            var ref2 = _selSpline.SplineParams;
            var wp  = _selKind == HandleKind.Knot
                ? WorldPos(ref2, _selEnd, wm)
                : WorldTangentTip(ref2, _selEnd, wm);
            SelectedHandleMatrix = Matrix4x4.CreateTranslation(wp);
            SelectedSpline = _selSpline;
        }

        // Draw curves first (behind everything)
        foreach (var sm in _chain)
        {
            ref var p  = ref sm.SplineParams;
            var     wm = sm.WorldMatrix;
            for (var i = 0; i < CurveSegs; i++)
            {
                var t0 = (float) i      / CurveSegs;
                var t1 = (float)(i + 1) / CurveSegs;
                var w0 = Vector3.Transform(UeToRenderer(EvalHermite(p, t0)), wm);
                var w1 = Vector3.Transform(UeToRenderer(EvalHermite(p, t1)), wm);
                if (TryProject(w0, vp, viewportMin, viewportSize, out var s0) &&
                    TryProject(w1, vp, viewportMin, viewportSize, out var s1))
                    drawList.AddLine(s0, s1, ColCurve, CurveThick);
            }
        }

        // Draw tangent arms + accumulate tangent handles only for selected knot
        if (_hasSelection && _selKind == HandleKind.Knot && _selSpline is not null)
        {
            ref var p  = ref _selSpline.SplineParams;
            var     wm = _selSpline.WorldMatrix;

            var sKnot  = default(Vector2);
            var sTang  = default(Vector2);
            var visKnot = TryProject(WorldPos(p, _selEnd, wm), vp, viewportMin, viewportSize, out sKnot);
            var visTang = TryProject(WorldTangentTip(p, _selEnd, wm), vp, viewportMin, viewportSize, out sTang);

            if (visKnot && visTang)
            {
                // Dashed arm
                DrawDashedLine(drawList, sKnot, sTang, ColArm, ArmThick);
            }

            if (visTang)
                _handles.Add(new Handle(HandleKind.Tangent, _selSpline, _selEnd, null, End.Start, sTang));
        }

        // Accumulate knot handles (built from chain, deduplicating shared knots)
        BuildKnotHandles(vp, viewportMin, viewportSize);
    }

    /// <summary>Hit-test, draw dots, handle input. Call once after DrawOverlay.</summary>
    public SplineOverlayAction EndFrame(
        ImDrawListPtr drawList,
        Vector2       viewportMin,
        Vector2       viewportSize)
    {
        var mouse      = ImGui.GetMousePos();
        var bestDistSq = float.MaxValue;
        _hasHover      = false;

        foreach (var h in _handles)
        {
            var hitR = h.Kind == HandleKind.Knot ? HitKnot : HitTang;
            var dx   = mouse.X - h.Screen.X;
            var dy   = mouse.Y - h.Screen.Y;
            var dSq  = dx * dx + dy * dy;
            if (dSq < hitR * hitR && dSq < bestDistSq)
            {
                bestDistSq = dSq;
                _hovSpline = h.Spline;
                _hovEnd    = h.End;
                _hovKind   = h.Kind;
                _hasHover  = true;
            }
        }

        // Draw dots (tangents first, knots on top)
        foreach (var h in _handles.OrderBy(h => h.Kind == HandleKind.Knot ? 1 : 0))
        {
            var isSel = _hasSelection && h.Spline == _selSpline && h.End == _selEnd && h.Kind == _selKind;
            var isHov = _hasHover     && h.Spline == _hovSpline && h.End == _hovEnd && h.Kind == _hovKind;

            if (h.Kind == HandleKind.Knot)
            {
                var col = isSel ? ColSelected : isHov ? ColHovered : ColKnot;
                DrawDiamond(drawList, h.Screen, KnotRadius, col);
            }
            else
            {
                var col = isSel ? ColSelected : isHov ? ColHovered : ColTangent;
                drawList.AddCircleFilled(h.Screen, TangRadius + 1.5f, ColDotBg);
                drawList.AddCircleFilled(h.Screen, TangRadius, col);
            }

            if (isSel || isHov)
            {
                var lc  = isSel ? ColSelected : ColHovered;
                var lbl = h.Kind == HandleKind.Knot ? $"{h.Spline.Name}" : $"Tangent";
                drawList.AddText(h.Screen + new Vector2(KnotRadius + 4f, -8f), lc, lbl);
            }
        }

        // Tooltip
        if (_hasHover && _hovSpline is not null)
        {
            ImGui.BeginTooltip();
            var kind = _hovKind == HandleKind.Knot ? (_hovEnd == End.Start ? "Start" : "End") : "Tangent";
            ImGui.TextUnformatted($"{_hovSpline.Name}  ·  {kind}");
            ImGui.EndTooltip();
        }

        var action          = SplineOverlayAction.None;
        var mouseInViewport = ImGui.IsMouseHoveringRect(viewportMin, viewportMin + viewportSize, false);

        if (mouseInViewport && _hasHover && _hovSpline is not null)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _selSpline    = _hovSpline;
                _selEnd       = _hovEnd;
                _selKind      = _hovKind;
                _hasSelection = true;
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                _ctxSpline = _hovSpline;
                _ctxEnd    = _hovEnd;
                _ctxKind   = _hovKind;
                ImGui.OpenPopup("##SplineCtx");
            }
        }

        // Context menu
        if (ImGui.BeginPopup("##SplineCtx") && _ctxSpline is not null)
        {
            var kind = _ctxKind == HandleKind.Knot
                ? (_ctxEnd == End.Start ? "Start Knot" : "End Knot")
                : "Tangent";
            ImGui.TextDisabled($"{_ctxSpline.Name}  ·  {kind}");
            ImGui.Separator();

            if (ImGui.MenuItem("\uf0e2  Reset this handle"))
            {
                ResetHandle(_ctxSpline, _ctxEnd, _ctxKind);
                action = SplineOverlayAction.Changed;
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem("\uf0e2  Reset all params for this spline"))
            {
                _ctxSpline.SplineParams = _originals.GetValueOrDefault(_ctxSpline);
                action = SplineOverlayAction.Changed;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        IsUsing = _hasHover || ImGui.IsPopupOpen("##SplineCtx");
        return action;
    }

    /// <summary>Apply an ImGuizmo translate result to the selected handle.</summary>
    public void ApplyGizmoMatrix(Matrix4x4 manipulated)
    {
        if (!_hasSelection || _selSpline is null) return;

        Matrix4x4.Invert(_selSpline.WorldMatrix, out var invWorld);
        var newLocal = RendererToUe(Vector3.Transform(manipulated.Translation, invWorld));

        ref var p = ref _selSpline.SplineParams;

        if (_selKind == HandleKind.Knot)
        {
            // Move position; keep tangent vectors fixed in local space (they move with the knot)
            if (_selEnd == End.Start)
            {
                p.StartPos = newLocal;

                // Propagate to the previous component's EndPos if they shared this knot
                var neighbor = FindNeighborAtStart(_selSpline);
                if (neighbor is not null)
                    neighbor.SplineParams.EndPos = newLocal;
            }
            else
            {
                p.EndPos = newLocal;

                // Propagate to the next component's StartPos
                var neighbor = FindNeighborAtEnd(_selSpline);
                if (neighbor is not null)
                    neighbor.SplineParams.StartPos = newLocal;
            }
        }
        else
        {
            // Tangent tip = pos + tangent
            if (_selEnd == End.Start)
                p.StartTangent = newLocal - p.StartPos;
            else
                p.EndTangent = newLocal - p.EndPos;
        }

        SelectedHandleMatrix = manipulated;
    }

    public void Reset()
    {
        _hasSelection        = false;
        _hasHover            = false;
        _selSpline           = null;
        SelectedSpline       = null;
        SelectedHandleMatrix = Matrix4x4.Identity;
        _originals.Clear();
        _handles.Clear();
        _chain.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chain helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Sort chain so that EndPos[i] ≈ StartPos[i+1]
    private void SortChain()
    {
        if (_chain.Count <= 1) return;

        var sorted = new List<SplineMeshComponent>(_chain.Count) { _chain[0] };
        var remaining = new List<SplineMeshComponent>(_chain.Skip(1));

        while (remaining.Count > 0)
        {
            var lastEnd = sorted[^1].SplineParams.EndPos;
            var best    = -1;
            var bestD   = float.MaxValue;
            for (var i = 0; i < remaining.Count; i++)
            {
                var d = Vector3.DistanceSquared(remaining[i].SplineParams.StartPos, lastEnd);
                if (d < bestD) { bestD = d; best = i; }
            }
            sorted.Add(remaining[best]);
            remaining.RemoveAt(best);
        }

        _chain.Clear();
        _chain.AddRange(sorted);
    }

    // Build knot handles, merging shared endpoint pairs into a single handle
    private void BuildKnotHandles(
        Matrix4x4 vp,
        Vector2   viewportMin,
        Vector2   viewportSize)
    {
        const float SharedThreshold = 0.01f * 0.01f; // squared distance

        for (var i = 0; i < _chain.Count; i++)
        {
            var sm = _chain[i];
            var wm = sm.WorldMatrix;
            ref var p = ref sm.SplineParams;

            // Start knot — only emit if not shared with previous component's end
            var isSharedStart = i > 0 &&
                Vector3.DistanceSquared(_chain[i - 1].SplineParams.EndPos, p.StartPos) < SharedThreshold;

            if (!isSharedStart)
            {
                if (TryProject(WorldPos(p, End.Start, wm), vp, viewportMin, viewportSize, out var sStart))
                    _handles.Add(new Handle(HandleKind.Knot, sm, End.Start, null, End.Start, sStart));
            }

            // End knot — always emit; if shared with next, store neighbor
            var neighbor     = (i + 1 < _chain.Count) ? _chain[i + 1] : null;
            var isSharedEnd  = neighbor is not null &&
                Vector3.DistanceSquared(p.EndPos, neighbor.SplineParams.StartPos) < SharedThreshold;

            if (TryProject(WorldPos(p, End.End, wm), vp, viewportMin, viewportSize, out var sEnd))
                _handles.Add(new Handle(HandleKind.Knot, sm, End.End,
                    isSharedEnd ? neighbor : null, End.Start, sEnd));
        }
    }

    private SplineMeshComponent? FindNeighborAtStart(SplineMeshComponent sm)
    {
        var idx = _chain.IndexOf(sm);
        if (idx <= 0) return null;
        var prev = _chain[idx - 1];
        return Vector3.DistanceSquared(prev.SplineParams.EndPos, sm.SplineParams.StartPos) < 0.01f * 0.01f
            ? prev : null;
    }

    private SplineMeshComponent? FindNeighborAtEnd(SplineMeshComponent sm)
    {
        var idx = _chain.IndexOf(sm);
        if (idx < 0 || idx >= _chain.Count - 1) return null;
        var next = _chain[idx + 1];
        return Vector3.DistanceSquared(sm.SplineParams.EndPos, next.SplineParams.StartPos) < 0.01f * 0.01f
            ? next : null;
    }

    private void ResetHandle(SplineMeshComponent sm, End end, HandleKind kind)
    {
        if (!_originals.TryGetValue(sm, out var orig)) return;
        ref var p = ref sm.SplineParams;
        if (kind == HandleKind.Knot)
        {
            if (end == End.Start) p.StartPos     = orig.StartPos;
            else                  p.EndPos        = orig.EndPos;
        }
        else
        {
            if (end == End.Start) p.StartTangent  = orig.StartTangent;
            else                  p.EndTangent    = orig.EndTangent;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Math / projection
    // ─────────────────────────────────────────────────────────────────────────

    private static Vector3 WorldPos(in SplineMeshParams p, End end, Matrix4x4 wm)
        => Vector3.Transform(UeToRenderer(end == End.Start ? p.StartPos : p.EndPos), wm);

    private static Vector3 WorldTangentTip(in SplineMeshParams p, End end, Matrix4x4 wm)
        => end == End.Start
            ? Vector3.Transform(UeToRenderer(p.StartPos + p.StartTangent), wm)
            : Vector3.Transform(UeToRenderer(p.EndPos   + p.EndTangent),   wm);

    private static Vector3 EvalHermite(in SplineMeshParams p, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return (2*t3 - 3*t2 + 1) * p.StartPos
             + (  t3 - 2*t2 + t) * p.StartTangent
             + (  t3 -   t2    ) * p.EndTangent
             + (-2*t3 + 3*t2   ) * p.EndPos;
    }

    private static Vector3 UeToRenderer(Vector3 v) => new(v.X, v.Z, v.Y);
    private static Vector3 RendererToUe(Vector3 v) => new(v.X, v.Z, v.Y);

    private static bool TryProject(
        Vector3 worldPos, Matrix4x4 viewProj,
        Vector2 viewportMin, Vector2 viewportSize,
        out Vector2 screenPos)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);
        if (clip.W <= 0f) { screenPos = default; return false; }
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        if (ndc.X < -1.1f || ndc.X > 1.1f || ndc.Y < -1.1f || ndc.Y > 1.1f || ndc.Z < 0 || ndc.Z > 1)
        { screenPos = default; return false; }
        screenPos = new Vector2(
            viewportMin.X + (ndc.X * 0.5f + 0.5f) * viewportSize.X,
            viewportMin.Y + (1f - (ndc.Y * 0.5f + 0.5f)) * viewportSize.Y);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drawing helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void DrawDiamond(ImDrawListPtr dl, Vector2 c, float r, uint col)
    {
        dl.AddQuadFilled(
            new Vector2(c.X,     c.Y - r),
            new Vector2(c.X + r, c.Y    ),
            new Vector2(c.X,     c.Y + r),
            new Vector2(c.X - r, c.Y    ),
            ColDotBg);
        dl.AddQuadFilled(
            new Vector2(c.X,         c.Y - r + 1.5f),
            new Vector2(c.X + r - 1.5f, c.Y        ),
            new Vector2(c.X,         c.Y + r - 1.5f),
            new Vector2(c.X - r + 1.5f, c.Y        ),
            col);
    }

    private static void DrawDashedLine(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float thick)
    {
        const float DashLen = 6f;
        const float GapLen  = 4f;
        var dir = b - a;
        var len = dir.Length();
        if (len < 0.001f) return;
        dir /= len;
        var t = 0f;
        var drawing = true;
        while (t < len)
        {
            var segLen = drawing ? DashLen : GapLen;
            var t1     = Math.Min(t + segLen, len);
            if (drawing)
                dl.AddLine(a + dir * t, a + dir * t1, col, thick);
            t       = t1;
            drawing = !drawing;
        }
    }
}
