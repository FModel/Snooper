using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Camera;

[DefaultActorSystem(typeof(CameraSystem))]
public sealed class CameraComponent : ActorComponent, IControllableComponent
{
    internal int PairIndex = -1;
    internal bool IsActive = false;
    
    public Matrix4x4 ViewMatrix = Matrix4x4.Identity;
    public Matrix4x4 ProjectionMatrix = Matrix4x4.Identity;
    public Matrix4x4 ViewProjectionMatrix = Matrix4x4.Identity;

    public CameraType Mode;
    public bool bFXAA = true;
    public bool bSSAO = false;
    public float SsaoRadius = 0.150f;
    public float SsaoBias = 0.05f;
    public float MovementSpeed = 1f;
    public float FieldOfView = 60.0f;
    public float FarPlaneDistance = 500f;
    public float NearPlaneDistance = 0.1f;
    public Vector2 ViewportSize = new(16, 9);

    public float FieldOfViewRadians => MathF.PI / 180.0f * FieldOfView;
    public float AspectRatio => ViewportSize.X / ViewportSize.Y;

    public void Update()
    {
        if (Actor is null) return;

        Matrix4x4.Decompose(Actor.Transform.WorldMatrix, out _, out var rotation, out var position);
        ViewMatrix = Matrix4x4.CreateLookAt(
            position,
            position + Vector3.Transform(Vector3.UnitZ, rotation),
            Vector3.Transform(Vector3.UnitY, rotation));

        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
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
        var panAxis = Vector3.Transform(-Vector3.UnitX, Actor.Transform.Rotation) * moveSpeed;
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

        if (keyboard.IsKeyDown(Keys.X))
        {
            FieldOfView = Math.Clamp(FieldOfView + 0.5f, 1.0f, 89.0f);
        }
        if (keyboard.IsKeyDown(Keys.C))
        {
            FieldOfView = Math.Clamp(FieldOfView - 0.5f, 1.0f, 89.0f);
        }
    }

    public void Update(float deltaX, float deltaY)
    {
        if (Actor is null) return;

        const float sensitivity = 0.001f;

        var yawRotation = Quaternion.CreateFromAxisAngle(-Vector3.UnitY, deltaX * sensitivity);
        var pitchRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, deltaY * sensitivity);

        Actor.Transform.Rotation = Quaternion.Normalize(yawRotation * Actor.Transform.Rotation * pitchRotation);
    }

    public void DrawControls()
    {
        ImGui.Checkbox("FXAA", ref bFXAA);
        ImGui.Checkbox("SSAO", ref bSSAO);
        ImGui.BeginDisabled(!bSSAO);
        ImGui.SliderFloat("Radius", ref SsaoRadius, 0.01f, 1.0f);
        ImGui.SliderFloat("Bias", ref SsaoBias, 0.0f, 0.1f);
        ImGui.EndDisabled();

        ImGui.DragFloat("Speed", ref MovementSpeed, 0.1f, 1f, 100f);
        ImGui.DragFloat("Near Plane", ref NearPlaneDistance, 0.001f, 0.001f, FarPlaneDistance - 1);
        ImGui.DragFloat("Far Plane", ref FarPlaneDistance, 0.1f , NearPlaneDistance + 1, 1000.0f);
    }

    public Vector2 ProjectToScreen(Vector3 worldPosition)
    {
        var clipSpacePosition = Vector4.Transform(new Vector4(worldPosition, 1.0f), ViewProjectionMatrix);
        var ndcSpacePosition = new Vector3(clipSpacePosition.X, clipSpacePosition.Y, clipSpacePosition.Z) / clipSpacePosition.W;

        return new Vector2(
            (ndcSpacePosition.X + 1.0f) * 0.5f * AspectRatio,
            (1.0f - ndcSpacePosition.Y) * 0.5f);
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

        return planes;
    }
}
