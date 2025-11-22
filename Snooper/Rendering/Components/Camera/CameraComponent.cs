using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Component;
using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Snooper.Core;
using Snooper.Rendering.Components.Transforms;
using Snooper.Rendering.Systems;
using Snooper.UI;

namespace Snooper.Rendering.Components.Camera;

[DefaultActorSystem(typeof(CameraSystem))]
public sealed class CameraComponent : SpatialComponent
{
    internal int PairIndex = -1;
    internal bool IsActive = false;
    
    public Matrix4x4 ViewMatrix = Matrix4x4.Identity;
    public Matrix4x4 ProjectionMatrix = Matrix4x4.Identity;
    public Matrix4x4 ViewProjectionMatrix = Matrix4x4.Identity;
    
    public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, LocalTransform.Rotation);
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, LocalTransform.Rotation);
    public Vector3 Right => Vector3.Transform(-Vector3.UnitX, LocalTransform.Rotation);

    public CameraType Mode;
    public bool bFXAA = true;
    public bool bAmbientOcclusion = true;
    public float SsaoRadius = 1.5f;
    public float MovementSpeed = 10f;
    public float FieldOfView = 89.0f;
    public float FarPlaneDistance = 10000f;
    public float NearPlaneDistance = 0.1f;
    public Vector2 ViewportSize = new(16, 9);

    public float FieldOfViewRadians => MathF.PI / 180.0f * FieldOfView;
    public float AspectRatio => ViewportSize.X / ViewportSize.Y;
    
    private Vector3 _velocity = Vector3.Zero;
    private Vector3? _teleportTarget = null;
    private Vector3 _teleportStart = Vector3.Zero;
    private float _teleportProgress = 0f;
    private const float TeleportDuration = 1f; // 1 second
    
    public CameraComponent(Transform? transform = null, string? name = null) : base(transform, name)
    {
        
    }
    
    public CameraComponent(UCameraComponent component) : base(component)
    {
        FieldOfView = component.GetOrDefault(nameof(FieldOfView), FieldOfView);
    }

    public void Update()
    {
        Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out var position);
        
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
        if (keyboard.IsKeyDown(Keys.W)) input.Z += 1;
        if (keyboard.IsKeyDown(Keys.S)) input.Z -= 1;
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

        if (keyboard.IsKeyDown(Keys.X)) FieldOfView = Math.Clamp(FieldOfView + 0.5f, 1.0f, 89.0f);
        if (keyboard.IsKeyDown(Keys.C)) FieldOfView = Math.Clamp(FieldOfView - 0.5f, 1.0f, 89.0f);
    }

    public void Update(float deltaX, float deltaY)
    {
        const float sensitivity = 0.001f;

        var yawRotation = Quaternion.CreateFromAxisAngle(-Vector3.UnitY, deltaX * sensitivity);
        var pitchRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, deltaY * sensitivity);

        LocalTransform.Rotation = Quaternion.Normalize(yawRotation * LocalTransform.Rotation * pitchRotation);
        MarkDirty(DirtyFlags.Transform);
    }
    
    internal override string Icon => "camera";

    public override void DrawControls()
    {
        base.DrawControls();
        
        EditorUI.CollapsingTable("Camera", ImGuiTreeNodeFlags.DefaultOpen, () =>
        {
            EditorUI.Checkbox("FXAA", ref bFXAA);
            EditorUI.Checkbox("Ambient Occlusion", ref bAmbientOcclusion);
            ImGui.BeginDisabled(!bAmbientOcclusion);
            EditorUI.Property("Radius");
            ImGui.SliderFloat("##Radius", ref SsaoRadius, 0.01f, 5.0f);
            ImGui.EndDisabled();

            EditorUI.DragFloat("Speed", ref MovementSpeed, 0.1f, 1f, 100f);
            EditorUI.DragFloat("FOV", ref FieldOfView, 0.1f, 1.0f, 89.0f);
            EditorUI.DragFloat("Near Plane", ref NearPlaneDistance, 0.001f, 0.001f, FarPlaneDistance - 1);
            EditorUI.DragFloat("Far Plane", ref FarPlaneDistance, 0.1f , NearPlaneDistance + 1, 1000.0f);
        });
    }

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

    public void TeleportTo(Vector3 targetPosition)
    {
        _teleportTarget = targetPosition;
        _teleportStart = LocalTransform.Position;
        _teleportProgress = 0f;
    }
}
