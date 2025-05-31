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
    public float FarPlaneDistance = 25.0f;
    public float NearPlaneDistance = 0.05f;
    public float AspectRatio = 16.0f / 9.0f;

    public float FieldOfViewRadians => MathF.PI / 180.0f * FieldOfView;

    public void Update()
    {
        if (Actor is null) return;

        Matrix4x4.Decompose(Actor.Transform.WorldMatrix, out _, out var rotation, out var position);
        ViewMatrix = Matrix4x4.CreateLookAtLeftHanded(
            position,
            position + Vector3.Transform(Vector3.UnitZ, rotation),
            Vector3.Transform(Vector3.UnitY, rotation));

        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
            FieldOfViewRadians,
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

    public Plane[] GetLocalFrustumPlanes() => GetFrustumPlanes(ProjectionMatrix);
    public Plane[] GetWorldFrustumPlanes() => GetFrustumPlanes(ViewProjectionMatrix);

    private Plane[] GetFrustumPlanes(Matrix4x4 matrix)
    {
        var planes = new Plane[6];

        planes[0] = new Plane(matrix.M14 + matrix.M11, matrix.M24 + matrix.M21, matrix.M34 + matrix.M31, matrix.M44 + matrix.M41); // Near
        planes[1] = new Plane(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21, matrix.M34 - matrix.M31, matrix.M44 - matrix.M41); // Far
        planes[2] = new Plane(matrix.M14 + matrix.M12, matrix.M24 + matrix.M22, matrix.M34 + matrix.M32, matrix.M44 + matrix.M42); // Left
        planes[3] = new Plane(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22, matrix.M34 - matrix.M32, matrix.M44 - matrix.M42); // Right
        planes[4] = new Plane(matrix.M14 + matrix.M13, matrix.M24 + matrix.M23, matrix.M34 + matrix.M33, matrix.M44 + matrix.M43); // Bottom
        planes[5] = new Plane(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23, matrix.M34 - matrix.M33, matrix.M44 - matrix.M43); // Top

        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = Plane.Normalize(planes[i]);
        }

        return planes;
    }
}
