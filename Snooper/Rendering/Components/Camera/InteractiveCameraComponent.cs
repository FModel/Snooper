using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Snooper.Rendering.Components.Camera;

public enum CameraType : byte
{
    Free,
    Orbital,
}

/// <summary>
/// camera that can move via keyboard and mouse input
/// </summary>
public class InteractiveCameraComponent : CameraComponent
{
    public CameraType ViewType { get; set; } = CameraType.Free;

    public float OrbitDistance
    {
        get;
        private set => field = MathF.Max(0.1f, value);
    } = 2.5f;

    public float MovementSpeed
    {
        get;
        set => field = MathF.Max(1f, value);
    } = 10f;

    private Vector3 _velocity = Vector3.Zero;
    private float _zoomVelocity = 0f;
    private Vector3? _teleportTarget = null;
    private Vector3 _teleportStart = Vector3.Zero;
    private float _teleportProgress = 0f;
    private float _currentTeleportDuration = 1f;

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
            _teleportProgress += time / _currentTeleportDuration;

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
        if (keyboard.IsKeyDown(Keys.A)) input.X -= 1;
        if (keyboard.IsKeyDown(Keys.D)) input.X += 1;
        if (keyboard.IsKeyDown(Keys.E)) input.Y += 1;
        if (keyboard.IsKeyDown(Keys.Q)) input.Y -= 1;

        const float smoothing = 12f; // higher = snappier
        var speed = MovementSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? 2f : 1f);

        if (ViewType == CameraType.Orbital)
        {
            var zoomTarget = 0f;
            if (keyboard.IsKeyDown(Keys.W)) zoomTarget = -speed;
            if (keyboard.IsKeyDown(Keys.S)) zoomTarget =  speed;
            _zoomVelocity += (zoomTarget - _zoomVelocity) * (1f - MathF.Exp(-smoothing * time));

            if (input != Vector3.Zero) input = Vector3.Normalize(input);
            var direction = (input.X * Right + input.Y * Up) * speed;
            _velocity = Vector3.Lerp(_velocity, direction, 1f - MathF.Exp(-smoothing * time));

            var orbitCenter = LocalTransform.Position - Forward * OrbitDistance;
            OrbitDistance += _zoomVelocity * time;
            orbitCenter += _velocity * time;
            LocalTransform.Position = orbitCenter + Forward * OrbitDistance;
        }
        else
        {
            if (keyboard.IsKeyDown(Keys.W)) input.Z -= 1;
            if (keyboard.IsKeyDown(Keys.S)) input.Z += 1;
            if (input != Vector3.Zero) input = Vector3.Normalize(input);

            var direction = (input.X * Right + input.Y * Up + input.Z * Forward) * speed;
            _velocity = Vector3.Lerp(_velocity, direction, 1f - MathF.Exp(-smoothing * time));
            LocalTransform.Position += _velocity * time;
        }

        MarkDirty(DirtyFlags.Transform);

        if (keyboard.IsKeyDown(Keys.X)) FieldOfView = Math.Clamp(FieldOfView + 0.5f, FieldOfViewMin, FieldOfViewMax);
        if (keyboard.IsKeyDown(Keys.C)) FieldOfView = Math.Clamp(FieldOfView - 0.5f, FieldOfViewMin, FieldOfViewMax);
    }

    public void Update(float deltaX, float deltaY)
    {
        const float sensitivity = 0.001f;

        var yawRotation = Quaternion.CreateFromAxisAngle(Settings.UpVector, -deltaX * sensitivity);

        switch (ViewType)
        {
            case CameraType.Free:
                var pitchRotation = Quaternion.CreateFromAxisAngle(Settings.RightVector, deltaY * sensitivity);
                LocalTransform.Rotation = Quaternion.Normalize(yawRotation * LocalTransform.Rotation * pitchRotation);
                break;
            case CameraType.Orbital:
            {
                const float maxPitch = MathF.PI / 2f - 0.01f;

                var newRotation = Quaternion.Normalize(yawRotation * LocalTransform.Rotation);
                var lookDir = -Vector3.Transform(Settings.ForwardVector, newRotation);
                var elevation = MathF.Asin(Math.Clamp(Vector3.Dot(lookDir, Settings.UpVector), -1f, 1f));

                var pitchDelta = -deltaY * sensitivity;
                var clampedElevation = Math.Clamp(elevation + pitchDelta, -maxPitch, maxPitch);
                var actualPitch = clampedElevation - elevation;

                var pitchRot = Quaternion.CreateFromAxisAngle(Settings.RightVector, -actualPitch);
                newRotation = Quaternion.Normalize(newRotation * pitchRot);

                var orbitCenter = LocalTransform.Position - Forward * OrbitDistance;
                var newForward = Vector3.Transform(Settings.ForwardVector, newRotation);
                LocalTransform.Rotation = newRotation;
                LocalTransform.Position = orbitCenter + newForward * OrbitDistance;
                break;
            }
        }

        MarkDirty(DirtyFlags.Transform);
    }

    private Quaternion? _snapTarget = null;
    private Quaternion _snapStart = Quaternion.Identity;
    private float _snapProgress = 0f;
    private const float SnapDuration = 0.25f;

    public void TeleportTo(Vector3 center, float orbitDistance)
    {
        ViewType = CameraType.Orbital;
        OrbitDistance = orbitDistance;
        _teleportTarget = center + Forward * orbitDistance;
        _teleportStart = LocalTransform.Position;
        float distance = Vector3.Distance(_teleportStart, _teleportTarget.Value);
        _currentTeleportDuration = MathF.Max(0.25f, MathF.Min(1f, distance / 10f));
        _teleportProgress = 0f;
    }

    public void SnapRotationTo(Quaternion targetRotation)
    {
        _snapStart    = LocalTransform.Rotation;
        _snapTarget   = targetRotation;
        _snapProgress = 0f;
    }
}
