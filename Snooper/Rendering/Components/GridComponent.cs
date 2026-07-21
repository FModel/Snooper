using System.Numerics;
using ImGuiNET;
using Snooper.Core;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components;

[DefaultActorSystem(typeof(GridSystem))]
public class GridComponent() : PrimitiveComponent(new Geometry())
{
    public override string Icon => "\uf850";

    public virtual Settings GridSettings { get; } = new();

    public class Settings
    {
        public float CellSize = 1.0f;
        public int CellsPerDivision = 11;

        public bool Adaptive = true;

        public float MinCellPixels = 8.0f;

        public float MinorThickness = 1.0f;
        public float MajorThickness = 1.5f;
        public float AxisThickness = 1.5f;

        public Vector3 MinorColor = Vector3.One;
        public Vector3 MajorColor = Vector3.One;
        public Vector3 AxisColorX = new(0.91f, 0.24f, 0.33f);
        public Vector3 AxisColorZ = new(0.19f, 0.61f, 0.98f);

        public float MinorOpacity = 0.25f;
        public float MajorOpacity = 0.5f;
        public float Opacity = 1.0f;
        public bool ShowAxes = false;

        public float FadeStart = 0.35f;
        public float FadeEnd = 1.0f;

        public Vector3 Tint = Vector3.One;
    }

    private class Geometry : PrimitiveData
    {
        public Geometry()
        {
            Vertices =
            [
                new Vector3(1.0f, 1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f, 1.0f, 0.0f)
            ];

            Indices =
            [
                0, 1, 3,
                1, 2, 3
            ];
        }
    }

    public override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Settings", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.ColorEdit3("Tint", ref GridSettings.Tint);

            if (GridSettings is OpaqueGridComponent.OpaqueSettings opaque)
            {
                EditorUI.ColorEdit3("Checker A", ref opaque.CheckerColorA);
                EditorUI.ColorEdit3("Checker B", ref opaque.CheckerColorB);
                EditorUI.SliderFloat("Checker Scale", ref opaque.CheckerScale, 0.25f, 8.0f, "%.2f per division");
                EditorUI.SliderFloat("Roughness", ref opaque.Roughness, 0.05f, 1.0f, "%.2f");
                EditorUI.SliderFloat("Metallic", ref opaque.Metallic, 0.0f, 1.0f, "%.2f");
            }
            else
            {
                EditorUI.SliderFloat("Opacity", ref GridSettings.Opacity, 0.0f, 1.0f, "%.2f");
            }

            EditorUI.SliderFloat("Fade Start", ref GridSettings.FadeStart, 0.0f, 1.0f, "%.2f of far plane");
            EditorUI.SliderFloat("Fade End", ref GridSettings.FadeEnd, 0.0f, 1.0f, "%.2f of far plane");

            EditorUI.DragFloat("Cell Size", ref GridSettings.CellSize, 0.01f, 0.0001f, 10000.0f, "%.3f units");
            EditorUI.SliderInt("Cells Per Division", ref GridSettings.CellsPerDivision, 2, 32, "%d cells");
            EditorUI.Checkbox("Adaptive", ref GridSettings.Adaptive);
            if (GridSettings.Adaptive)
            {
                EditorUI.SliderFloat("Min Cell Size", ref GridSettings.MinCellPixels, 1.0f, 64.0f, "%.0f px");
            }

            EditorUI.ColorEdit3("Minor Color", ref GridSettings.MinorColor);
            EditorUI.SliderFloat("Minor Opacity", ref GridSettings.MinorOpacity, 0.0f, 1.0f, "%.2f");
            EditorUI.SliderFloat("Minor Thickness", ref GridSettings.MinorThickness, 0.1f, 10.0f, "%.2f px");

            EditorUI.ColorEdit3("Major Color", ref GridSettings.MajorColor);
            EditorUI.SliderFloat("Major Opacity", ref GridSettings.MajorOpacity, 0.0f, 1.0f, "%.2f");
            EditorUI.SliderFloat("Major Thickness", ref GridSettings.MajorThickness, 0.1f, 10.0f, "%.2f px");

            EditorUI.Checkbox("Show Axes", ref GridSettings.ShowAxes);
            if (!GridSettings.ShowAxes) return;
            EditorUI.ColorEdit3("X Axis Color", ref GridSettings.AxisColorX);
            EditorUI.ColorEdit3("Z Axis Color", ref GridSettings.AxisColorZ);
            EditorUI.SliderFloat("Axis Thickness", ref GridSettings.AxisThickness, 0.1f, 10.0f, "%.2f px");
        });
    }
}

public class OpaqueGridComponent : GridComponent
{
    protected override bool SupportsOpaquePass => true;

    public override Settings GridSettings { get; } = new OpaqueSettings();

    public class OpaqueSettings : Settings
    {
        public Vector3 CheckerColorA = new(0.16f);
        public Vector3 CheckerColorB = new(0.09f);

        public float CheckerScale = 1.0f;
        public float Roughness = 0.95f;
        public float Metallic;

        public OpaqueSettings()
        {
            Tint = new Vector3(0.08f);
        }
    }
}
