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

/// <summary>
/// just a fixed camera
/// </summary>
[DefaultActorSystem(typeof(CameraSystem))]
public class CameraComponent : SpatialComponent, IViewProjectionProvider, IResizable
{
    protected const float FieldOfViewMin = 30.0f;
    protected const float FieldOfViewMax = 150.0f;

    protected override DirtyFlags SupportedDirtyFlags => base.SupportedDirtyFlags | DirtyFlags.Projection;

    public float Width { get; private set; } = 16.0f;
    public float Height { get; private set; } = 9.0f;
    public float FieldOfView
    {
        get;
        set
        {
            if (field.Equals(value)) return;

            field = value;
            MarkDirty(DirtyFlags.Projection);
        }
    } = 110.0f;
    public float AspectRatio
    {
        get;
        private set
        {
            if (field.Equals(value)) return;

            field = value;
            MarkDirty(DirtyFlags.Projection);
        }
    } = 1.777778f;

    public float OrthoWidth { get; } = 15.0f;
    public float OrthoNearClipPlane { get; private set; } = 0.01f;
    public float OrthoFarClipPlane { get; private set; } = 20000.0f;

    public float PerspectiveNearClipPlane { get; private set; } = 0.01f;
    public float PerspectiveFarClipPlane { get; private set; } = 20000.0f;

    public CameraMode ProjectionMode { get; } = CameraMode.Perspective;

    public Matrix4x4 ViewMatrix
    {
        get;
        private set
        {
            field = value;
            InverseViewMatrix = Matrix4x4.Invert(field, out var inverse) ? inverse : Matrix4x4.Identity;
        }
    } = Matrix4x4.Identity;
    public Matrix4x4 ProjectionMatrix
    {
        get;
        private set
        {
            field = value;
            InverseProjectionMatrix = Matrix4x4.Invert(field, out var inverse) ? inverse : Matrix4x4.Identity;
        }
    } = Matrix4x4.Identity;
    public Matrix4x4 InverseViewMatrix { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 InverseProjectionMatrix { get; private set; } = Matrix4x4.Identity;

    public float FieldOfViewRadians => 2.0f * MathF.Atan(MathF.Tan(float.DegreesToRadians(FieldOfView) * 0.5f) / _aspectRatio);
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
            if (NearClipPlane.Equals(value)) return;

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
            MarkDirty(DirtyFlags.Projection);
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
            if (FarClipPlane.Equals(value)) return;

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
            MarkDirty(DirtyFlags.Projection);
        }
    }

    public Vector3 Forward => Vector3.Transform(Settings.ForwardVector, LocalTransform.Rotation);
    public Vector3 Up => Vector3.Transform(Settings.UpVector, LocalTransform.Rotation);
    public Vector3 Right => Vector3.Cross(Up, Forward);

    // the horizontal FieldOfView becomes vertical through this, not the viewport's aspect, else resizing a panel zooms
    private readonly float _aspectRatio = 1.777778f;

    public CameraComponent(UCameraComponent component) : base(component)
    {
        FieldOfView = component.FieldOfView;
        _aspectRatio = component.AspectRatio;

        OrthoWidth = component.OrthoWidth * Settings.GlobalScale;
        OrthoNearClipPlane = component.OrthoNearClipPlane * Settings.GlobalScale;
        OrthoFarClipPlane = component.OrthoFarClipPlane * Settings.GlobalScale;

        ProjectionMode = component.ProjectionMode == ECameraProjectionMode.Orthographic ? CameraMode.Orthographic : CameraMode.Perspective;

        // forward axis difference, don't ask why only here (same for DirectionalLightComponent)
        LocalTransform.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    }

    protected CameraComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {

    }

    public void UpdateMatrices()
    {
        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out var position);

        ViewMatrix = Matrix4x4.CreateLookAt(position, position - Vector3.Transform(Settings.ForwardVector, rotation), Vector3.Transform(Settings.UpVector, rotation));
        var projection = ProjectionMode switch
        {
            CameraMode.Orthographic => Matrix4x4.CreateOrthographic(OrthoWidth * AspectRatio, OrthoWidth, OrthoNearClipPlane, OrthoFarClipPlane),
            CameraMode.Perspective => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, AspectRatio, PerspectiveNearClipPlane, PerspectiveFarClipPlane),
            _ => throw new ArgumentOutOfRangeException()
        };

        var reverse = Matrix4x4.Identity;
        reverse.M33 = -1.0f;
        reverse.M43 = 1.0f;
        ProjectionMatrix = projection * reverse;

        MarkClean(DirtyFlags.Projection);
    }

    public override string Icon => "\uf030";

    public sealed override void DrawControls()
    {
        base.DrawControls();

        EditorUI.CollapsingTable("Camera", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            var fov = FieldOfView;
            if (EditorUI.DragFloat("FOV", ref fov, 0.1f, FieldOfViewMin, FieldOfViewMax, "%.2f deg")) FieldOfView = fov;

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
