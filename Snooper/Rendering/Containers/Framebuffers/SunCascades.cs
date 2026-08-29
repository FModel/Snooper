using System.Numerics;
using Serilog;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.UI;

namespace Snooper.Rendering.Containers.Framebuffers;

public sealed class SunCascades
{
    public float Lambda = 0.85f;
    public float MaxDistance = 150.0f;
    public float CasterDistance = 100.0f;

    public ShadowMapView[] Views { get; private set; } = [];
    public float[] Splits { get; private set; } = [];
    public int FirstSlot { get; internal set; }

    public int CascadeCount => Views.Length;

    private float _lastLambda;
    private float _lastMaxDistance;
    private float _lastNear;
    private float _lastFar;

    public SunCascades(int cascadeCount)
    {
        SetCascadeCount(cascadeCount);
    }

    public void SetCascadeCount(int cascadeCount)
    {
        cascadeCount = Math.Clamp(cascadeCount, 1, Settings.MaxShadowCascades);
        if (cascadeCount == CascadeCount) return;

        Views = new ShadowMapView[cascadeCount];
        Splits = new float[Views.Length];

        _lastLambda = float.NaN; // force update the splits, and so every cascade, to be recomputed
    }

    public ShadowMapView[] Update(CameraComponent camera, DirectionalLightComponent light, int resolution, uint mask)
    {
        if (UpdateSplits(camera)) mask = uint.MaxValue;

        Matrix4x4.Decompose(light.WorldMatrix, out _, out var rotation, out _);
        var toLight = Vector3.Normalize(Vector3.Transform(Settings.ForwardVector, rotation));

        // never camera.Up: the look-at degenerates into a NaN matrix the moment the camera up vector
        // lines up with the light, which is what used to blank out a cascade at certain angles. this
        // only switches when the sun comes within a few degrees of vertical and depends on nothing but
        // the sun, so it stays put frame to frame
        var up = MathF.Abs(Vector3.Dot(toLight, Settings.UpVector)) > 0.99f ? Settings.RightVector : Settings.UpVector;

        // rotation only light space, anchored at the world origin. the texel grid we snap to lives in
        // here, so unlike a basis centred on the cascade it does not travel with the camera
        var lightBasis = Matrix4x4.CreateLookAt(Vector3.Zero, -toLight, up);
        Matrix4x4.Invert(lightBasis, out var lightBasisInverse);

        var tanHalfFov = MathF.Tan(camera.FieldOfViewRadians * 0.5f);
        var aspect = camera.AspectRatio;

        for (var i = 0; i < CascadeCount; i++)
        {
            if ((mask & (1u << i)) == 0) continue; // held back, keep the view its depth map was rendered with

            var near = i == 0 ? camera.NearClipPlane : Splits[i - 1];
            var far = Splits[i];

            var (distance, radius) = FitSphere(near, far);
            radius = MathF.Ceiling(radius * 16.0f) / 16.0f; // so a drifting fov or aspect cannot jitter the texel size

            var centerWorld = Vector3.Transform(new Vector3(0.0f, 0.0f, -distance), camera.InverseViewMatrix);
            var centerLight = Vector3.Transform(centerWorld, lightBasis);

            // snap the centre onto whole texels of the fixed grid, so camera movement translates the
            // cascade in texel steps instead of sliding it under the samples
            var texelWorldSize = radius * 2.0f / resolution;
            centerLight.X = MathF.Floor(centerLight.X / texelWorldSize) * texelWorldSize;
            centerLight.Y = MathF.Floor(centerLight.Y / texelWorldSize) * texelWorldSize;
            centerLight.Z = MathF.Floor(centerLight.Z / texelWorldSize) * texelWorldSize;

            var snappedWorld = Vector3.Transform(centerLight, lightBasisInverse);

            // the eye has to be a real point near the cascade: the culler reads
            // InverseViewMatrix.Translation off this view
            var depthRange = radius * 2.0f + CasterDistance;
            var eye = snappedWorld + toLight * (radius + CasterDistance);
            var viewMatrix = Matrix4x4.CreateLookAt(eye, snappedWorld, up);

            // symmetric by construction, the sphere sits at (0, 0, -(radius + CasterDistance))
            var projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                -radius, radius,
                -radius, radius,
                0.0f, depthRange);

            // System.Numerics emits a D3D style [0, 1] clip depth while this context still runs GL's
            // default [-1, 1] clip range, so window depth only ever spans [0.5, 1.0]
            var depthScale = 0.5f / depthRange;

            Views[i] = new ShadowMapView(viewMatrix, projectionMatrix, FirstSlot + i, texelWorldSize, depthScale, far);
        }

        return Views;

        (float Distance, float Radius) FitSphere(float near, float far)
        {
            var k2 = tanHalfFov * tanHalfFov * (1.0f + aspect * aspect);

            // the far corners dominate, so the sphere is centred on the far plane
            if (k2 * (far + near) >= far - near)
                return (far, far * MathF.Sqrt(k2));

            var distance = 0.5f * (far + near) * (1.0f + k2);
            var radius = 0.5f * MathF.Sqrt(
                (far - near) * (far - near) +
                2.0f * (far * far + near * near) * k2 +
                (far + near) * (far + near) * k2 * k2);

            return (distance, radius);
        }
    }

    private bool UpdateSplits(CameraComponent camera)
    {
        if (MathF.Abs(_lastLambda - Lambda) < float.Epsilon &&
            MathF.Abs(_lastMaxDistance - MaxDistance) < float.Epsilon &&
            MathF.Abs(_lastNear - camera.NearClipPlane) < float.Epsilon &&
            MathF.Abs(_lastFar - camera.FarClipPlane) < float.Epsilon)
        {
            return false;
        }

        _lastLambda = Lambda;
        _lastMaxDistance = MaxDistance;
        _lastNear = camera.NearClipPlane;
        _lastFar = camera.FarClipPlane;

        var near = _lastNear;
        var far = MathF.Min(_lastMaxDistance, _lastFar);

        for (var i = 0; i < CascadeCount; i++)
        {
            var p = (i + 1) / (float) CascadeCount;
            var log = near * MathF.Pow(far / near, p);
            var lin = near + (far - near) * p;

            Splits[i] = float.Lerp(lin, log, _lastLambda);
        }

        Log.Debug("Updated shadow cascade splits: {Splits}", Splits);
        return true;
    }

    public void DrawControls()
    {
        EditorUI.DragFloat("Distance", ref MaxDistance, 1.0f, 1.0f, 10000.0f, "%.0f units");
        EditorUI.DragFloat("Distribution", ref Lambda, 0.01f, 0.0f, 1.0f, "%.2f");
        EditorUI.DragFloat("Caster Distance", ref CasterDistance, 1.0f, 1.0f, 10000.0f, "%.0f units");
    }
}
