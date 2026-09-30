using System.Numerics;
using ImGuiNET;
using ImGuizmoNET;
using Snooper.Rendering.Components.Camera;

namespace Editor.Widgets;

// cells stacked down from the viewport's top right corner, a widget is centred in the one it takes
public sealed class ViewportGrid
{
    private Vector2 _topRight;
    private int _row;

    public void Reset(Vector2 topRight)
    {
        _topRight = topRight;
        _row = 0;
    }

    public Vector2 Cell()
    {
        var size = ImGui.GetFrameHeight() * 5f;
        var spacing = ImGui.GetFrameHeight() * 0.5f;
        var first = spacing + size * 0.5f;

        return _topRight + new Vector2(-first, first + _row++ * (size + spacing));
    }
}

public readonly struct ViewportContext(InteractiveCameraComponent camera, ImDrawListPtr drawList, Vector2 position, Vector2 size, OPERATION operation, MODE mode, ViewportGrid grid)
{
    public readonly InteractiveCameraComponent Camera = camera;
    public readonly ImDrawListPtr DrawList = drawList;
    public readonly Vector2 Position = position;
    public readonly Vector2 Size = size;
    public readonly OPERATION Operation = operation;
    public readonly MODE Mode = mode;
    private readonly ViewportGrid _grid = grid;

    public Vector2 Cell() => _grid.Cell();

    public bool Manipulate(ref Matrix4x4 matrix) => Manipulate(ref matrix, Operation, Mode);
    public bool Manipulate(ref Matrix4x4 matrix, OPERATION operation, MODE mode)
    {
        if (!ImGuizmo.IsUsing() && Camera.ProjectionMode != CameraMode.Orthographic)
        {
            var viewPosition = Vector3.Transform(matrix.Translation, Camera.ViewMatrix);
            if (viewPosition.Z >= -Camera.NearClipPlane) return false; // because out depth is reversed
        }

        var view = Camera.ViewMatrix;
        var projection = Camera.ProjectionMatrix;
        return ImGuizmo.Manipulate(ref view.M11, ref projection.M11, operation, mode, ref matrix.M11);
    }
}
