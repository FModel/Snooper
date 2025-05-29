using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Camera;

[DefaultActorSystem(typeof(CameraFrustumSystem))]
public class CameraFrustumComponent(CameraComponent camera) : PrimitiveComponent(new Geometry(camera))
{
    private struct Geometry : IPrimitiveData
    {
        public float[] Vertices { get; }
        public uint[] Indices { get; }

        public Geometry(CameraComponent camera)
        {
            var frustumLength = camera.FarPlaneDistance;

            var aspect = camera.AspectRatio;
            var nearPlaneWidth = camera.NearPlaneDistance * aspect;
            var nearPlaneHeight = camera.NearPlaneDistance;
            var farPlaneWidth = camera.FarPlaneDistance * aspect;
            var farPlaneHeight = camera.FarPlaneDistance;

            // Calculate half dimensions for near and far planes
            float nearHalfWidth = nearPlaneWidth / 2.0f;
            float nearHalfHeight = nearPlaneHeight / 2.0f;
            float farHalfWidth = farPlaneWidth / 2.0f;
            float farHalfHeight = farPlaneHeight / 2.0f;

            Vertices =
            [
                // Near plane vertices
                -nearHalfWidth, nearHalfHeight, 0.0f, // Top-left
                nearHalfWidth, nearHalfHeight, 0.0f, // Top-right
                nearHalfWidth, -nearHalfHeight, 0.0f, // Bottom-right
                -nearHalfWidth, -nearHalfHeight, 0.0f, // Bottom-left

                // Far plane vertices
                -farHalfWidth, farHalfHeight, frustumLength, // Top-left
                farHalfWidth, farHalfHeight, frustumLength, // Top-right
                farHalfWidth, -farHalfHeight, frustumLength, // Bottom-right
                -farHalfWidth, -farHalfHeight, frustumLength // Bottom-left
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

    public void Update()
    {
        var frustumLength = camera.FarPlaneDistance;

        var aspect = camera.AspectRatio;
        var nearPlaneWidth = camera.NearPlaneDistance * aspect;
        var nearPlaneHeight = camera.NearPlaneDistance;
        var farPlaneWidth = camera.FarPlaneDistance * aspect;
        var farPlaneHeight = camera.FarPlaneDistance;

        float nearHalfWidth = nearPlaneWidth / 2.0f;
        float nearHalfHeight = nearPlaneHeight / 2.0f;
        float farHalfWidth = farPlaneWidth / 2.0f;
        float farHalfHeight = farPlaneHeight / 2.0f;

        float[] vertices =
        [
            // Near plane vertices
            -nearHalfWidth, nearHalfHeight, 0.0f, // Top-left
            nearHalfWidth, nearHalfHeight, 0.0f, // Top-right
            nearHalfWidth, -nearHalfHeight, 0.0f, // Bottom-right
            -nearHalfWidth, -nearHalfHeight, 0.0f, // Bottom-left

            // Far plane vertices
            -farHalfWidth, farHalfHeight, frustumLength, // Top-left
            farHalfWidth, farHalfHeight, frustumLength, // Top-right
            farHalfWidth, -farHalfHeight, frustumLength, // Bottom-right
            -farHalfWidth, -farHalfHeight, frustumLength // Bottom-left
        ];

        VBO.Bind();
        VBO.SetData(vertices);
    }
}
