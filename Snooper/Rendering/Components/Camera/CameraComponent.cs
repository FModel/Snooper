using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Camera;

public enum CameraMode : byte
{
    Perspective,
    Orthographic
}

public enum CameraType : byte
{
    Free,
    Orbital,
}

/// <summary>
/// just a fixed camera
/// </summary>
[DefaultActorSystem(typeof(CameraSystem))]
public class CameraComponent : SpatialComponent, IViewProjectionProvider, IResizable
{
    protected const float FieldOfViewMin = 30.0f;
    protected const float FieldOfViewMax = 120.0f;

    public float FieldOfView = 90.0f;

    public float Width { get; private set; } = 16.0f;
    public float Height { get; private set; } = 9.0f;
    public float AspectRatio { get; private set; } = 1.777778f;

    public float OrthoWidth { get; } = 15.0f;
    public float OrthoNearClipPlane { get; private set; } = 0.1f;
    public float OrthoFarClipPlane { get; private set; } = 10000.0f;

    public float PerspectiveNearClipPlane { get; private set; } = 0.1f;
    public float PerspectiveFarClipPlane { get; private set; } = 10000.0f;

    public CameraMode ProjectionMode { get; } = CameraMode.Perspective;

    public Matrix4x4 ViewMatrix { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 ProjectionMatrix { get; private set; } = Matrix4x4.Identity;

    public float FieldOfViewRadians => MathF.PI / 180.0f * FieldOfView;
    public float NearClipPlane
    {
        get => ProjectionMode switch
        {
            CameraMode.Orthographic => OrthoNearClipPlane,
            CameraMode.Perspective => PerspectiveNearClipPlane,
            _ => throw new ArgumentOutOfRangeException()
        };
        set
        {
            switch (ProjectionMode)
            {
                case CameraMode.Orthographic:
                    OrthoNearClipPlane = value;
                    break;
                case CameraMode.Perspective:
                    PerspectiveNearClipPlane = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    public float FarClipPlane
    {
        get => ProjectionMode switch
        {
            CameraMode.Orthographic => OrthoFarClipPlane,
            CameraMode.Perspective => PerspectiveFarClipPlane,
            _ => throw new ArgumentOutOfRangeException()
        };
        set
        {
            switch (ProjectionMode)
            {
                case CameraMode.Orthographic:
                    OrthoFarClipPlane = value;
                    break;
                case CameraMode.Perspective:
                    PerspectiveFarClipPlane = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public Vector3 Forward => Vector3.Transform(Settings.ForwardVector, LocalTransform.Rotation);
    public Vector3 Up => Vector3.Transform(Settings.UpVector, LocalTransform.Rotation);
    public Vector3 Right => Vector3.Cross(Up, Forward);

    public CameraComponent(UCameraComponent component) : base(component)
    {
        FieldOfView = component.GetOrDefault(nameof(FieldOfView), FieldOfView);
        AspectRatio = component.GetOrDefault(nameof(AspectRatio), AspectRatio);

        OrthoWidth = component.GetOrDefault(nameof(OrthoWidth), OrthoWidth);
        OrthoNearClipPlane = component.GetOrDefault(nameof(OrthoNearClipPlane), OrthoNearClipPlane);
        OrthoFarClipPlane = component.GetOrDefault(nameof(OrthoFarClipPlane), OrthoFarClipPlane);

        PerspectiveNearClipPlane = component.GetOrDefault(nameof(PerspectiveNearClipPlane), PerspectiveNearClipPlane);
        PerspectiveFarClipPlane = component.GetOrDefault(nameof(PerspectiveFarClipPlane), PerspectiveFarClipPlane);

        ProjectionMode = component.GetOrDefault(nameof(ProjectionMode), ProjectionMode);
    }

    public CameraComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    public void UpdateMatrices()
    {
        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out var position);

        ViewMatrix = Matrix4x4.CreateLookAt(position, position - Vector3.Transform(Settings.ForwardVector, rotation), Vector3.Transform(Settings.UpVector, rotation));
        ProjectionMatrix = ProjectionMode switch
        {
            CameraMode.Orthographic => Matrix4x4.CreateOrthographic(OrthoWidth * AspectRatio, OrthoWidth, OrthoNearClipPlane, OrthoFarClipPlane),
            CameraMode.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, AspectRatio, PerspectiveNearClipPlane, PerspectiveFarClipPlane),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    internal override string Icon => "\uf030";

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Camera", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.DragFloat("FOV", ref FieldOfView, 0.1f, FieldOfViewMin, FieldOfViewMax, "%.2f deg");

            var nearClip = NearClipPlane;
            var farClip = FarClipPlane;

            var edited = EditorUI.DragFloat("Near Clip Plane", ref nearClip, 0.1f, 0.01f, farClip - 0.1f);
            edited |= EditorUI.DragFloat("Far Clip Plane", ref farClip, 1.0f, nearClip + 0.1f, 100000.0f);

            if (edited)
            {
                NearClipPlane = nearClip;
                FarClipPlane = farClip;
            }
        });
    }

    public void Resize(int newWidth, int newHeight)
    {
        Width = newWidth;
        Height = newHeight;
        AspectRatio = Width / Height;
    }
}
