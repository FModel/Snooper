using System.Numerics;
using ImGuiNET;
using Snooper;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.UI;

namespace Editor.Widgets;

public class SunOverlayWidget : IViewportTool<DirectionalLightComponent>
{
    private static float Radius => 2.0f * ImGui.GetFrameHeight();

    private const float Mark = 0.18f;
    private const float Outline = 0.07f;
    private const float Tick = 0.3f;
    private const float Ambient = 0.12f;
    private const int Rings = 8;
    private const int Segments = 32;

    private const uint ColOutline = 0xAA_00_00_00;
    private const uint ColGuide = 0x44_FF_FF_FF;
    private const uint ColCamera = 0xFF_FF_FF_FF;

    private bool _dragging;

    public void Reset() => _dragging = false;

    public bool Draw(in ViewportContext viewport, DirectionalLightComponent light)
    {
        var center = viewport.Cell();
        var drawList = viewport.DrawList;
        var camera = viewport.Camera;

        var unit = ImGui.GetFrameHeight();
        var size = Radius;
        var outline = Outline * unit;

        var inverseView = camera.InverseViewMatrix;
        var ahead = new Vector3(-inverseView.M31, 0.0f, -inverseView.M33);
        if (ahead.LengthSquared() < 1e-4f) ahead = new Vector3(inverseView.M21, 0.0f, inverseView.M23);
        ahead = Vector3.Normalize(ahead);
        var aside = Vector3.Cross(ahead, Settings.UpVector);

        Matrix4x4.Decompose(light.WorldMatrix, out var scale, out var rotation, out var position);
        var toLight = Vector3.Normalize(Vector3.Transform(Settings.ForwardVector, rotation));

        var mouse = (ImGui.GetMousePos() - center) / size;
        var hovered = ImGui.IsWindowHovered() && mouse.LengthSquared() <= 1.0f; // not through a window lying over the viewport

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _dragging = true;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _dragging = false;

        if (_dragging)
        {
            var turn = Between(toLight, Sky(mouse));
            if (!turn.IsIdentity)
            {
                rotation = Quaternion.Normalize(Quaternion.Concatenate(rotation, turn));
                toLight = Vector3.Normalize(Vector3.Transform(Settings.ForwardVector, rotation));

                light.ApplyGizmoMatrix(Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position));
            }
        }

        DrawSky();
        DrawGuides();
        DrawSun();

        if (hovered || _dragging)
        {
            EditorUI.Tooltip("Hold the left mouse button to put the sun under the mouse");
        }

        return hovered || _dragging;

        // the direction in the sky a point of the disc stands for: the elevation falls evenly from the centre to the rim
        Vector3 Sky(Vector2 point)
        {
            var radius = MathF.Min(point.Length(), 1.0f);
            var heading = radius > 1e-4f ? Vector2.Normalize(point) : Vector2.Zero;
            var (sin, cos) = MathF.SinCos((1.0f - radius) * MathF.PI * 0.5f);

            return aside * (heading.X * cos) + Settings.UpVector * sin - ahead * (heading.Y * cos); // the screen's y goes down
        }

        // the point of the disc a direction stands at, on the rim when it is below the horizon
        Vector2 OnSky(Vector3 direction)
        {
            var heading = new Vector2(Vector3.Dot(direction, aside), -Vector3.Dot(direction, ahead));
            if (heading.LengthSquared() < 1e-8f) return Vector2.Zero;

            var elevation = MathF.Asin(Math.Clamp(Vector3.Dot(direction, Settings.UpVector), -1.0f, 1.0f));
            return Vector2.Normalize(heading) * Math.Clamp(1.0f - elevation / (MathF.PI * 0.5f), 0.0f, 1.0f);
        }

        // the shortest rotation from one direction to the other, which leaves the light's roll alone
        Quaternion Between(Vector3 from, Vector3 to)
        {
            var dot = Vector3.Dot(from, to);
            if (dot > 0.999999f) return Quaternion.Identity;
            if (dot < -0.9999f)
            {
                var axis = Vector3.Cross(from, MathF.Abs(from.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX);
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
            }

            var cross = Vector3.Cross(from, to);
            return Quaternion.Normalize(new Quaternion(cross.X, cross.Y, cross.Z, 1.0f + dot));
        }

        // the disc as patches, each one as bright as its part of the sky is close to the sun
        void DrawSky()
        {
            var flags = drawList.Flags;
            drawList.Flags &= ~ImDrawListFlags.AntiAliasedFill; // the patches share their edges, the fringe would draw the mesh

            for (var ring = 0; ring < Rings; ring++)
            {
                var inner = (float) ring / Rings;
                var outer = (float) (ring + 1) / Rings;

                for (var segment = 0; segment < Segments; segment++)
                {
                    var from = segment * MathF.Tau / Segments;
                    var to = (segment + 1) * MathF.Tau / Segments;

                    var patch = Sky(OnDisc((from + to) * 0.5f, (inner + outer) * 0.5f));
                    var lit = Ambient + (1.0f - Ambient) * MathF.Max(Vector3.Dot(patch, toLight), 0.0f);

                    drawList.AddQuadFilled(
                        center + OnDisc(from, inner) * size, center + OnDisc(from, outer) * size,
                        center + OnDisc(to, outer) * size, center + OnDisc(to, inner) * size,
                        ImGui.GetColorU32(new Vector4(light.Color * lit, 1.0f)));
                }
            }

            drawList.Flags = flags;
            drawList.AddCircle(center, size, ColOutline, 0, outline);
        }

        void DrawGuides()
        {
            drawList.AddCircle(center, size * 2.0f / 3.0f, ColGuide, 0, outline * 0.5f); // 30 degrees above the horizon
            drawList.AddCircle(center, size / 3.0f, ColGuide, 0, outline * 0.5f); // 60 degrees

            // what the camera sees of the horizon: a wedge as wide as its field of view, which an orthographic one lacks
            var projection = camera.ProjectionMatrix;
            if (projection.M44 == 0.0f)
            {
                var (sin, cos) = MathF.SinCos(MathF.Atan(1.0f / projection.M11));
                drawList.AddLine(center, center + new Vector2(-sin, -cos) * size, ColGuide, outline * 0.5f);
                drawList.AddLine(center, center + new Vector2(sin, -cos) * size, ColGuide, outline * 0.5f);
            }

            drawList.AddLine(center - Vector2.UnitY * (size - Tick * unit), center - Vector2.UnitY * size, ColCamera, outline); // straight ahead
        }

        // hollow when it is below the horizon. Orange ringed with black, to read on the bright side as well as on the dark one
        void DrawSun()
        {
            var mark = center + OnSky(toLight) * size;
            var color = ImGui.GetColorU32(Settings.OrangeColor);

            drawList.AddCircle(mark, Mark * unit + outline * 0.5f, 0xFF_00_00_00, 0, outline);
            if (Vector3.Dot(toLight, Settings.UpVector) >= 0.0f) drawList.AddCircleFilled(mark, Mark * unit, color);
            else drawList.AddCircle(mark, Mark * unit - outline * 0.5f, color, 0, outline);
        }

        Vector2 OnDisc(float angle, float radius)
        {
            var (sin, cos) = MathF.SinCos(angle);
            return new Vector2(cos, sin) * radius;
        }
    }
}
