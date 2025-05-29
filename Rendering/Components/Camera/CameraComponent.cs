using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Camera;

[DefaultActorSystem(typeof(CameraSystem))]
public sealed class CameraComponent : ActorComponent
{
    public Matrix4x4 ViewMatrix = Matrix4x4.Identity;
    public Matrix4x4 ProjectionMatrix = Matrix4x4.Identity;
    public Matrix4x4 ViewProjectionMatrix = Matrix4x4.Identity;

    public CameraType Mode = CameraType.FlyCamera;
    public float MovementSpeed = 1f;
    public float FieldOfView = 60.0f;
    public float FarPlaneDistance = 50.0f;
    public float NearPlaneDistance = 0.1f;
    public float AspectRatio = 16.0f / 9.0f;

    public void Update()
    {
        if (Actor is null) return;

        Matrix4x4.Decompose(Actor.Transform.WorldMatrix, out _, out var rotation, out var position);
        ViewMatrix = Matrix4x4.CreateLookAtLeftHanded(
            position,
            position + Vector3.Transform(Vector3.UnitZ, rotation),
            Vector3.Transform(Vector3.UnitY, rotation));

        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
            FieldOfView * (float)(Math.PI / 180.0f),
            AspectRatio,
            NearPlaneDistance,
            FarPlaneDistance);

        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }

    public void Update(KeyboardState keyboard, float time)
    {
        if (!keyboard.IsAnyKeyDown || Actor is null) return;

        var multiplier = keyboard.IsKeyDown(Keys.LeftShift) ? 2f : 1f;
        var moveSpeed = MovementSpeed * multiplier * time;

        var moveAxis = Vector3.Transform(Vector3.UnitZ, Actor.Transform.Rotation) * moveSpeed;
        var panAxis = Vector3.Transform(Vector3.UnitX, Actor.Transform.Rotation) * moveSpeed;
        var up = Vector3.Transform(Vector3.UnitY, Actor.Transform.Rotation) * moveSpeed;

        if (keyboard.IsKeyDown(Keys.W))
        {
            Actor.Transform.Position += moveAxis;
        }
        if (keyboard.IsKeyDown(Keys.S))
        {
            Actor.Transform.Position -= moveAxis;
        }
        if (keyboard.IsKeyDown(Keys.A))
        {
            Actor.Transform.Position -= panAxis;
        }
        if (keyboard.IsKeyDown(Keys.D))
        {
            Actor.Transform.Position += panAxis;
        }
        if (keyboard.IsKeyDown(Keys.E))
        {
            Actor.Transform.Position += up;
        }
        if (keyboard.IsKeyDown(Keys.Q))
        {
            Actor.Transform.Position -= up;
        }
    }

    public void Update(Vector2 mouseDelta)
    {
        if (Actor is null) return;

        const float sensitivity = 0.001f;

        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, mouseDelta.X * sensitivity);
        var pitchRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, mouseDelta.Y * sensitivity);

        Actor.Transform.Rotation = Quaternion.Normalize(yawRotation * Actor.Transform.Rotation * pitchRotation);
    }
}
