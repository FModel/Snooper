using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.UI;

namespace Snooper.Rendering.Containers.Framebuffers;

public class ShadowFramebuffer(int size, int cascadeCount) : Framebuffer, IControllable
{
    public override int Width => _cascades.Width;
    public override int Height => _cascades.Height;
    public int CascadeCount => _cascades.Depth;
    public float Bias = 0.001f;

    public readonly float[] CascadePlaneDistances = new float[cascadeCount];
    public readonly Matrix4x4[] CascadeMatrices = new Matrix4x4[cascadeCount];

    private readonly Texture2DArray _cascades = new(size, size, cascadeCount, SizedInternalFormat.DepthComponent16, PixelFormat.DepthComponent, PixelType.Float);
    private float _lambda = 0.85f;
    private float _maxFarPlane = 150.0f;

    // cache for dirty checks
    private float _lastLambda;
    private float _lastMaxFarPlane;
    private float _lastNearClipPlane;
    private float _lastFarClipPlane;

    public override void Generate()
    {
        _cascades.Generate();
        _cascades.Reset<int>(Width, Height, []);
        GL.TextureParameter(_cascades, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
        GL.TextureParameter(_cascades, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        GL.TextureParameter(_cascades, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_cascades, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);
        GL.TextureParameter(_cascades, TextureParameterName.TextureBorderColor, [1.0f, 1.0f, 1.0f, 1.0f]);

        base.Generate();
        GL.NamedFramebufferTexture(Handle, FramebufferAttachment.DepthAttachment, _cascades, 0);
        GL.NamedFramebufferDrawBuffer(Handle, DrawBufferMode.None);
        GL.NamedFramebufferReadBuffer(Handle, ReadBufferMode.None);

        CheckStatus();
    }

    public IViewProjectionProvider[] UpdateCascades(CameraComponent camera, DirectionalLightComponent light)
    {
        UpdatePlaneDistances(camera);
        return UpdateViewProjectionProvider(camera, light);
    }

    private void UpdatePlaneDistances(CameraComponent camera)
    {
        if (MathF.Abs(_lastLambda - _lambda) < float.Epsilon &&
            MathF.Abs(_lastMaxFarPlane - _maxFarPlane) < float.Epsilon &&
            MathF.Abs(_lastNearClipPlane - camera.NearClipPlane) < float.Epsilon &&
            MathF.Abs(_lastFarClipPlane - camera.FarClipPlane) < float.Epsilon)
        {
            return;
        }

        _lastLambda = _lambda;
        _lastMaxFarPlane = _maxFarPlane;
        _lastNearClipPlane = camera.NearClipPlane;
        _lastFarClipPlane = camera.FarClipPlane;

        var near = _lastNearClipPlane;
        var far = MathF.Min(_lastMaxFarPlane, _lastFarClipPlane);

        for (int i = 0; i < CascadeCount; i++)
        {
            var p = (i + 1) / (float)CascadeCount;
            var log = near * MathF.Pow(far / near, p);
            var lin = near + (far - near) * p;

            CascadePlaneDistances[i] = float.Lerp(lin, log, _lastLambda);
        }

        Log.Debug("Updated shadow cascade plane distances: {Distances}", CascadePlaneDistances);
    }

    private IViewProjectionProvider[] UpdateViewProjectionProvider(CameraComponent camera, DirectionalLightComponent light)
    {
        Matrix4x4.Decompose(light.WorldMatrix, out _, out var rotation, out _);
        var lightDir = Vector3.Transform(Settings.ForwardVector, rotation);

        Matrix4x4.Invert(camera.ViewMatrix, out var invView);
        var aspect = camera.AspectRatio;
        var tanHalfFov = MathF.Tan(camera.FieldOfViewRadians * 0.5f);
        var up = camera.Up;

        var cascadeCameras = new IViewProjectionProvider[CascadeCount];
        for (var cascadeIndex = 0; cascadeIndex < cascadeCameras.Length; cascadeIndex++)
        {
            var cascadeNear = cascadeIndex == 0 ? _lastNearClipPlane : CascadePlaneDistances[cascadeIndex - 1];
            var cascadeFar = CascadePlaneDistances[cascadeIndex];

            var nearHeight = 2.0f * tanHalfFov * cascadeNear;
            var nearWidth = nearHeight * aspect;
            var farHeight = 2.0f * tanHalfFov * cascadeFar;
            var farWidth = farHeight * aspect;

            var frustumCorners = new Vector3[]
            {
                // near plane
                new(-nearWidth / 2,  nearHeight / 2, -cascadeNear),
                new( nearWidth / 2,  nearHeight / 2, -cascadeNear),
                new( nearWidth / 2, -nearHeight / 2, -cascadeNear),
                new(-nearWidth / 2, -nearHeight / 2, -cascadeNear),
                // far plane
                new(-farWidth / 2,  farHeight / 2, -cascadeFar),
                new( farWidth / 2,  farHeight / 2, -cascadeFar),
                new( farWidth / 2, -farHeight / 2, -cascadeFar),
                new(-farWidth / 2, -farHeight / 2, -cascadeFar),
            };

            for (var i = 0; i < frustumCorners.Length; i++)
            {
                frustumCorners[i] = Vector3.Transform(frustumCorners[i], invView);
            }

            var center = Vector3.Zero;
            foreach (var corner in frustumCorners)
            {
                center += corner;
            }
            center /= frustumCorners.Length;

            var radius = 0.0f;
            foreach (var corner in frustumCorners)
            {
                float distance = Vector3.Distance(corner, center);
                radius = MathF.Max(radius, distance);
            }

            var lightPos = center + lightDir * (radius * 2.0f);
            var viewMatrix = Matrix4x4.CreateLookAt(lightPos, center, up);

            var lightSpaceCorners = new Vector3[frustumCorners.Length];
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                lightSpaceCorners[i] = Vector3.Transform(frustumCorners[i], viewMatrix);
            }

            var min = lightSpaceCorners[0];
            var max = lightSpaceCorners[0];
            for (var i = 1; i < lightSpaceCorners.Length; i++)
            {
                min = Vector3.Min(min, lightSpaceCorners[i]);
                max = Vector3.Max(max, lightSpaceCorners[i]);
            }

            var casterExtension = radius * 1.5f;
            min.Z -= casterExtension;
            max.Z += casterExtension;

            var extent = MathF.Max(max.X - min.X, max.Y - min.Y) * 0.5f;
            extent = MathF.Ceiling(extent * 16f) / 16f; // snap to 1/16 units

            var worldUnitsPerTexel = extent * 2f / Width;
            var centerLs = (min + max) * 0.5f;

            centerLs.X = MathF.Floor(centerLs.X / worldUnitsPerTexel) * worldUnitsPerTexel;
            centerLs.Y = MathF.Floor(centerLs.Y / worldUnitsPerTexel) * worldUnitsPerTexel;

            var left   = centerLs.X - extent;
            var right  = centerLs.X + extent;
            var bottom = centerLs.Y - extent;
            var top    = centerLs.Y + extent;
            var nearZ = -max.Z;
            var farZ  = -min.Z;

            var projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                left, right,
                bottom, top,
                nearZ, farZ
            );

            cascadeCameras[cascadeIndex] = new ShadowViewProjectionProvider(viewMatrix, projectionMatrix);
            CascadeMatrices[cascadeIndex] = viewMatrix * projectionMatrix;
        }

        return cascadeCameras;
    }

    public override void Bind(uint texture, uint unit) => Bind(unit);
    public override void Bind(uint unit) => _cascades.Bind(unit);

    public override void Resize(int newWidth, int newHeight)
    {
        // shadow map size is fixed
    }

    public override Texture[] GetTextures() => [];

    public void DrawControls()
    {
        EditorUI.PropertyValueTable("Shadows", () =>
        {
            EditorUI.Text("Resolution", $"{Width} px");
            EditorUI.DragFloat("Lambda", ref _lambda, 0.01f, 0.0f, 1.0f, "%.2f");
            EditorUI.DragFloat("Max Far Plane", ref _maxFarPlane, 1.0f, 1.0f, 10000.0f, "%.1f units");
            EditorUI.DragFloat("Bias", ref Bias, 0.000001f, 0.000005f, 0.05f, "%.6f");
            EditorUI.Text("Cascade Count", $"{CascadeCount}");
            EditorUI.Text("Cascade Planes", $"{string.Join(", ", CascadePlaneDistances)} units");
        });
    }

    public override long Allocated
    {
        get
        {
            long total = 0;
            total += _cascades.Allocated;
            return total;
        }
    }

    public override long Used
    {
        get
        {
            long total = 0;
            total += _cascades.Used;
            return total;
        }
    }

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Cascade Textures", _cascades);
    }

    public override void Dispose()
    {
        base.Dispose();

        _cascades.Dispose();
    }
}
