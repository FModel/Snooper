using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Snooper.Rendering.Components.Camera;

/// <summary>
/// camera that can move via keyboard and mouse input
/// </summary>
public class InteractiveCameraComponent : CameraComponent
{
    public CameraType ViewType { get; set; } = CameraType.Free;

    public float MovementSpeed
    {
        get;
        set => field = MathF.Max(1f, value);
    } = 10f;

    private Vector3 _velocity = Vector3.Zero;
    private Vector3? _teleportTarget = null;
    private Vector3 _teleportStart = Vector3.Zero;
    private float _teleportProgress = 0f;
    private const float TeleportDuration = 1f; // 1 second

    public void Update(KeyboardState keyboard, float time)
    {
        // Handle smooth rotation snap
        if (_snapTarget.HasValue)
        {
            _snapProgress += time / SnapDuration;
            if (_snapProgress >= 1f)
            {
                LocalTransform.Rotation = _snapTarget.Value;
                _snapTarget   = null;
                _snapProgress = 0f;
            }
            else
            {
                var t = _snapProgress * _snapProgress * (3f - 2f * _snapProgress);
                LocalTransform.Rotation = Quaternion.Normalize(Quaternion.Slerp(_snapStart, _snapTarget.Value, t));
            }
            MarkDirty(DirtyFlags.Transform);
        }

        // Handle smooth teleportation
        if (_teleportTarget.HasValue)
        {
            _teleportProgress += time / TeleportDuration;

            if (_teleportProgress >= 1f)
            {
                LocalTransform.Position = _teleportTarget.Value;
                MarkDirty(DirtyFlags.Transform);
                _teleportTarget = null;
                _velocity = Vector3.Zero;
                _teleportProgress = 0f;
            }
            else
            {
                // SmoothStep interpolation for smooth easing
                var t = _teleportProgress * _teleportProgress * (3f - 2f * _teleportProgress);
                LocalTransform.Position = Vector3.Lerp(_teleportStart, _teleportTarget.Value, t);
                MarkDirty(DirtyFlags.Transform);
                return; // Skip manual input during teleportation
            }
        }

        var input = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) input.Z -= 1;
        if (keyboard.IsKeyDown(Keys.S)) input.Z += 1;
        if (keyboard.IsKeyDown(Keys.A)) input.X -= 1;
        if (keyboard.IsKeyDown(Keys.D)) input.X += 1;
        if (keyboard.IsKeyDown(Keys.E)) input.Y += 1;
        if (keyboard.IsKeyDown(Keys.Q)) input.Y -= 1;
        if (input != Vector3.Zero) input = Vector3.Normalize(input);

        var speed = MovementSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? 2f : 1f);
        var direction = (input.X * Right + input.Y * Up + input.Z * Forward) * speed;

        const float smoothing = 12f; // higher = snappier
        _velocity = Vector3.Lerp(_velocity, direction, 1f - MathF.Exp(-smoothing * time));

        LocalTransform.Position += _velocity * time;
        MarkDirty(DirtyFlags.Transform);

        if (keyboard.IsKeyDown(Keys.X)) FieldOfView = Math.Clamp(FieldOfView + 0.5f, FieldOfViewMin, FieldOfViewMax);
        if (keyboard.IsKeyDown(Keys.C)) FieldOfView = Math.Clamp(FieldOfView - 0.5f, FieldOfViewMin, FieldOfViewMax);
    }

    public void Update(float deltaX, float deltaY)
    {
        const float sensitivity = 0.001f;

        var yawRotation = Quaternion.CreateFromAxisAngle(Settings.UpVector, -deltaX * sensitivity);
        var pitchRotation = Quaternion.CreateFromAxisAngle(Settings.RightVector, deltaY * sensitivity);

        LocalTransform.Rotation = Quaternion.Normalize(yawRotation * LocalTransform.Rotation * pitchRotation);
        MarkDirty(DirtyFlags.Transform);
    }

    private Quaternion? _snapTarget = null;
    private Quaternion _snapStart = Quaternion.Identity;
    private float _snapProgress = 0f;
    private const float SnapDuration = 0.25f;

    public void TeleportTo(Vector3 targetPosition)
    {
        _teleportTarget = targetPosition;
        _teleportStart = LocalTransform.Position;
        _teleportProgress = 0f;
    }

    public void SnapRotationTo(Quaternion targetRotation)
    {
        _snapStart    = LocalTransform.Rotation;
        _snapTarget   = targetRotation;
        _snapProgress = 0f;
    }
}
