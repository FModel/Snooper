using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components.Camera;

namespace Editor.Widgets;

public class ViewportAxisWidget
{
    private const float BigCircleRadius  = 54f;
    private const float AxisLineLength   = BigCircleRadius - CircleRadius;
    private const float CircleRadius     = 10f;
    private const float CircleHoverBoost = 1.6f;
    private const float LineWidth        = 2.5f;
    private const float LineHoverBoost   = 1.35f;
    private const float OutlineWidth     = 2f;
    private const float Margin           = 12f;

    private const float FadeFactor          = 0.34f;
    private const float HoverFadeRate       = 14f;
    private const float NegLabelFadeRate    = 10f;
    private const float AnimEpsilon         = 0.0005f;

    private record AxisInfo(uint Color, Vector3 Direction, string Label, bool IsPrimary);

    private static readonly AxisInfo[] _axes =
    [
        new(Settings.AxisColorX, Vector3.UnitX, "X", true),
        new(Settings.AxisColorX, -Vector3.UnitX, "-X", false),
        new(Settings.AxisColorY, Vector3.UnitY, "Y", true),
        new(Settings.AxisColorY, -Vector3.UnitY, "-Y", false),
        new(Settings.AxisColorZ, Vector3.UnitZ, "Z", true),
        new(Settings.AxisColorZ, -Vector3.UnitZ, "-Z", false),
    ];

    private readonly float[] _hoverFade = new float[6];
    private readonly float[] _negLabelFade = new float[6]; // only odd indices used

    public int HoveredAxis = -1;
    public readonly Quaternion[] SnapRotations;

    public ViewportAxisWidget()
    {
        SnapRotations = new Quaternion[_axes.Length];
        for (var i = 0; i < SnapRotations.Length; i++)
        {
            var forward = _axes[i].Direction;

            // choose a reference up that isn't parallel to forward.
            var refUp = MathF.Abs(Vector3.Dot(forward, Settings.UpVector)) < 0.9999f ? Settings.UpVector : -Settings.ForwardVector;
            var right = Vector3.Normalize(Vector3.Cross(refUp, forward));
            var up = Vector3.Cross(forward, right);

            SnapRotations[i] = Quaternion.CreateFromRotationMatrix(new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, forward.X, forward.Y, forward.Z, 0f, 0f, 0f, 0f, 1f));
        }
    }

    public void Update(float delta)
    {
        for (var i = 0; i < _hoverFade.Length; i++)
        {
            _hoverFade[i] = StepToward(_hoverFade[i], HoveredAxis == i ? 1f : 0f, HoverFadeRate, delta);
            if ((i & 1) == 1) // negative axes only
                _negLabelFade[i] = StepToward(_negLabelFade[i], HoveredAxis == i ? 1f : 0f, NegLabelFadeRate, delta);
        }
    }

    public bool Draw(IViewProjectionProvider camera, Vector2 position)
    {
        var drawList = ImGui.GetWindowDrawList();

        var center = new Vector2(position.X - BigCircleRadius - Margin, position.Y + BigCircleRadius + Margin);
        var right = new Vector3(camera.InverseViewMatrix.M11, camera.InverseViewMatrix.M12, camera.InverseViewMatrix.M13);
        var up = new Vector3(camera.InverseViewMatrix.M21, camera.InverseViewMatrix.M22, camera.InverseViewMatrix.M23);
        var forward = new Vector3(camera.InverseViewMatrix.M31, camera.InverseViewMatrix.M32, camera.InverseViewMatrix.M33);
        Vector2 Project(Vector3 dir) => center + new Vector2(Vector3.Dot(dir, right), -Vector3.Dot(dir, up)) * AxisLineLength;

        HoveredAxis = -1;

        var mouse = ImGui.GetMousePos();
        var mouseInWidget = (mouse - center).LengthSquared() <= BigCircleRadius * BigCircleRadius;
        if (mouseInWidget)
        {
            var bestDepth = float.MinValue;
            for (var i = 0; i < _axes.Length; i++)
            {
                var tip = Project(_axes[i].Direction);
                var dx  = mouse.X - tip.X;
                var dy  = mouse.Y - tip.Y;
                if (dx * dx + dy * dy <= CircleRadius * CircleRadius)
                {
                    var d = Vector3.Dot(_axes[i].Direction, forward);
                    if (d > bestDepth)
                    {
                        bestDepth = d;
                        HoveredAxis = i;
                    }
                }
            }
        }

        var order = new[] { 0, 1, 2, 3, 4, 5 };
        Array.Sort(order, (a, b) => Vector3.Dot(_axes[a].Direction, forward).CompareTo(Vector3.Dot(_axes[b].Direction, forward)));

        // arms
        foreach (var id in order)
        {
            var axis = _axes[id];
            if (!axis.IsPrimary) continue;

            var hover = _hoverFade[id];
            var depth = Vector3.Dot(axis.Direction, forward);
            var colorFac = FadeFactor + (1f - FadeFactor) * ((depth + 1f) * 0.5f);
            var tip = Project(axis.Direction);
            var animRadius = CircleRadius + CircleHoverBoost * hover;

            var radial = tip - center;
            var lineEnd = radial.Length() > 0.00001f ? tip - Vector2.Normalize(radial) * animRadius : tip;

            drawList.AddLine(center, lineEnd, WithAlpha(axis.Color, colorFac), LineWidth + LineHoverBoost * hover);
        }

        // dots and labels
        foreach (var id in order)
        {
            var axis = _axes[id];
            var hover = _hoverFade[id];
            var depth = Vector3.Dot(axis.Direction, forward);
            var colorFac = FadeFactor + (1f - FadeFactor) * ((depth + 1f) * 0.5f);
            var tip = Project(axis.Direction);
            var animRadius = CircleRadius + CircleHoverBoost * hover;

            uint fillCol;
            uint outlineCol = 0;
            if (axis.IsPrimary)
            {
                fillCol = LerpColor(WithAlpha(axis.Color, colorFac), 0xFF_FF_FF_FF, 0.08f * hover);
            }
            else
            {
                fillCol = WithAlpha(Darken(axis.Color, 0.2f), colorFac * 0.92f);
                outlineCol = LerpColor(WithAlpha(axis.Color, colorFac * 0.95f), 0xFF_FF_FF_FF, 0.12f * hover);
            }

            if (!axis.IsPrimary) drawList.AddCircle(tip, animRadius + OutlineWidth * 0.5f, outlineCol, 0, OutlineWidth);
            drawList.AddCircleFilled(tip, animRadius, fillCol);

            var labelAlpha = axis.IsPrimary ? 1f : _negLabelFade[id];
            if (labelAlpha <= AnimEpsilon) continue;

            var hoverWhiten = 0.85f * hover;
            var baseLabelCol = WithAlpha(0xE6_18_12_0E, labelAlpha);
            var labelCol = LerpColor(baseLabelCol, 0xFF_FF_FF_FF, hoverWhiten);
            var labelSize = ImGui.CalcTextSize(axis.Label);
            drawList.AddText(tip - labelSize * 0.5f, labelCol, axis.Label);
        }

        if (HoveredAxis >= 0)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted($"Snap to {_axes[HoveredAxis].Label}");
            ImGui.EndTooltip();
        }

        return mouseInWidget && HoveredAxis >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private float StepToward(float value, float target, float rate, float dt)
    {
        if (dt <= 0f) return value;
        return value + (target - value) * (1f - MathF.Exp(-rate * dt));
    }

    private uint WithAlpha(uint col, float alphaScale)
    {
        var a = (byte)((col >> 24) * Math.Clamp(alphaScale, 0f, 1f));
        return (col & 0x00_FF_FF_FF) | ((uint)a << 24);
    }

    private uint Darken(uint col, float factor)
    {
        factor = 1f - Math.Clamp(factor, 0f, 1f);
        var r = (byte)(((col >>  0) & 0xFF) * factor);
        var g = (byte)(((col >>  8) & 0xFF) * factor);
        var b = (byte)(((col >> 16) & 0xFF) * factor);
        var a =        (col >> 24) & 0xFF;
        return (a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
    }

    private uint LerpColor(uint ca, uint cb, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        byte Lerp(uint mask, int shift) => (byte)(((ca >> shift) & mask) + (((cb >> shift) & mask) - ((ca >> shift) & mask)) * t);
        return ((uint)Lerp(0xFF, 24) << 24)
             | ((uint)Lerp(0xFF, 16) << 16)
             | ((uint)Lerp(0xFF,  8) <<  8)
             |  Lerp(0xFF,  0);
    }
}
