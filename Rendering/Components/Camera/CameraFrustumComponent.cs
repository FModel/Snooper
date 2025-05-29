using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Camera;

[DefaultActorSystem(typeof(CameraFrustumSystem))]
public class CameraFrustumComponent(CameraComponent cameraComponent) : PrimitiveComponent(new Geometry(cameraComponent))
{
    // protected override PolygonMode PolygonMode { get => PolygonMode.Line; }

    private struct Geometry : IPrimitiveData
    {
        public Vector3[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CameraComponent cameraComponent)
        {
            var frustumLength = cameraComponent.FarPlaneDistance - cameraComponent.NearPlaneDistance;

            var aspect = cameraComponent.AspectRatio;
            var nearPlaneWidth = cameraComponent.NearPlaneDistance * aspect;
            var nearPlaneHeight = cameraComponent.NearPlaneDistance;
            var farPlaneWidth = cameraComponent.FarPlaneDistance * aspect;
            var farPlaneHeight = cameraComponent.FarPlaneDistance;

            // Calculate half dimensions for near and far planes
            float nearHalfWidth = nearPlaneWidth / 2.0f;
            float nearHalfHeight = nearPlaneHeight / 2.0f;
            float farHalfWidth = farPlaneWidth / 2.0f;
            float farHalfHeight = farPlaneHeight / 2.0f;

            Vertices =
            [
                new Vector3(-nearHalfWidth, nearHalfHeight, 0.0f), // Top-left near
                new Vector3(nearHalfWidth, nearHalfHeight, 0.0f), // Top-right near
                new Vector3(nearHalfWidth, -nearHalfHeight, 0.0f), // Bottom-right near
                new Vector3(-nearHalfWidth, -nearHalfHeight, 0.0f), // Bottom-left near

                new Vector3(-farHalfWidth, farHalfHeight, frustumLength), // Top-left far
                new Vector3(farHalfWidth, farHalfHeight, frustumLength), // Top-right far
                new Vector3(farHalfWidth, -farHalfHeight, frustumLength), // Bottom-right far
                new Vector3(-farHalfWidth, -farHalfHeight, frustumLength) // Bottom-left far
            ];

            Indices =
            [
                // Near plane (two triangles)
                0, 1, 2,
                2, 3, 0,

                // Far plane (two triangles)
                4, 6, 5,
                6, 4, 7,

                // Sides (four trapezoids, each made of two triangles)
                // Top side
                0, 4, 1,
                1, 4, 5,

                // Right side
                1, 5, 2,
                2, 5, 6,

                // Bottom side
                2, 6, 3,
                3, 6, 7,

                // Left side
                3, 7, 0,
                0, 7, 4
            ];
        }
    }

    public override void Update()
    {
        var frustumLength = cameraComponent.FarPlaneDistance - cameraComponent.NearPlaneDistance;

        var aspect = cameraComponent.AspectRatio;
        var nearPlaneWidth = cameraComponent.NearPlaneDistance * aspect;
        var nearPlaneHeight = cameraComponent.NearPlaneDistance;
        var farPlaneWidth = cameraComponent.FarPlaneDistance * aspect;
        var farPlaneHeight = cameraComponent.FarPlaneDistance;

        // Calculate half dimensions for near and far planes
        float nearHalfWidth = nearPlaneWidth / 2.0f;
        float nearHalfHeight = nearPlaneHeight / 2.0f;
        float farHalfWidth = farPlaneWidth / 2.0f;
        float farHalfHeight = farPlaneHeight / 2.0f;

        Vector3[] vertices =
        [
            new Vector3(-nearHalfWidth, nearHalfHeight, 0.0f), // Top-left near
            new Vector3(nearHalfWidth, nearHalfHeight, 0.0f), // Top-right near
            new Vector3(nearHalfWidth, -nearHalfHeight, 0.0f), // Bottom-right near
            new Vector3(-nearHalfWidth, -nearHalfHeight, 0.0f), // Bottom-left near

            new Vector3(-farHalfWidth, farHalfHeight, frustumLength), // Top-left far
            new Vector3(farHalfWidth, farHalfHeight, frustumLength), // Top-right far
            new Vector3(farHalfWidth, -farHalfHeight, frustumLength), // Bottom-right far
            new Vector3(-farHalfWidth, -farHalfHeight, frustumLength) // Bottom-left far
        ];

        VBO.Bind();
        VBO.SetData(vertices);
    }
}
