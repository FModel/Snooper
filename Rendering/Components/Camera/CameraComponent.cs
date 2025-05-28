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

    public float MovementSpeed = 1f;
    public float FieldOfView = 60.0f;
    public float FarPlaneDistance = 100000.0f;
    public float NearPlaneDistance = 0.1f;
    public float AspectRatio = 16.0f / 9.0f;

    public void Update()
    {
        if (Actor is null) return;
        Matrix4x4.Decompose(Actor.Transform.WorldMatrix, out _, out Quaternion rotation, out Vector3 translation);

        var forwardVector = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
        var upVector = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotation));
        ViewMatrix = Matrix4x4.CreateLookAtLeftHanded(translation, translation + forwardVector, upVector);

        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView * (float)(Math.PI / 180.0f),
            AspectRatio,
            NearPlaneDistance,
            FarPlaneDistance);

        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }

    public void Update(KeyboardState keyboard, float time)
    {
        if (!keyboard.IsAnyKeyDown || Actor is null) return;
        Matrix4x4.Decompose(ViewMatrix, out _, out Quaternion rotation, out _);

        var multiplier = keyboard.IsKeyDown(Keys.LeftShift) ? 2f : 1f;
        var moveSpeed = MovementSpeed * multiplier * time;
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation));
        var right = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, rotation));

        if (keyboard.IsKeyDown(Keys.W))
        {
            Actor.Transform.Position += forward * moveSpeed;
        }
        if (keyboard.IsKeyDown(Keys.S))
        {
            Actor.Transform.Position -= forward * moveSpeed;
        }
        if (keyboard.IsKeyDown(Keys.A))
        {
            Actor.Transform.Position -= right * moveSpeed;
        }
        if (keyboard.IsKeyDown(Keys.D))
        {
            Actor.Transform.Position += right * moveSpeed;
        }
        if (keyboard.IsKeyDown(Keys.E))
        {
            Actor.Transform.Position += Vector3.UnitY * moveSpeed;
        }
        if (keyboard.IsKeyDown(Keys.Q))
        {
            Actor.Transform.Position -= Vector3.UnitY * moveSpeed;
        }
    }
}
