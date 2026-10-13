using System.Numerics;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Rendering.Components.Visualization;

public class CameraComponentVisualization : DebugComponent
{
    private const float Depth = 0.5f;

    private readonly CameraComponent _camera;

    public CameraComponentVisualization(CameraComponent camera) : base(Settings.Camera, name: $"{camera.Name} (Frustum)")
    {
        _camera = camera;
        Descriptor = new PrimitiveDescriptor<Vector3>(new CullingBounds(Vector3.One), () => new Geometry());
    }

    public override Matrix4x4[] GetWorldMatrices(int index = -1)
    {
        var halfHeight = MathF.Tan(_camera.FieldOfViewRadians * 0.5f) * Depth;
        return [Matrix4x4.CreateScale(halfHeight * _camera.AspectRatio, halfHeight, Depth) * WorldMatrix];
    }

    private class Geometry : DebugGeometry
    {
        public Geometry()
        {
            Vertices =
            [
                Vector3.Zero,
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
                new Vector3(-0.6f, 1.15f, 1), new Vector3(0.6f, 1.15f, 1), new Vector3(0, 1.7f, 1)
            ];

            Indices =
            [
                0, 1,  0, 2,  0, 3,  0, 4,
                1, 2,  2, 3,  3, 4,  4, 1,
                5, 6,  6, 7,  7, 5,
            ];
        }
    }
}
