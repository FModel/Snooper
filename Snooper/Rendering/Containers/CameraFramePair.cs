using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Textures;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Containers.Framebuffers;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Containers;

public class CameraFramePair(CameraComponent camera) : IResizable, IMemoryDetailsProvider
{
    private const int DefaultWidthHeight = 1;

    public bool IsOpen = true;

    public CameraComponent Camera { get; } = camera;

    private readonly GeometryBuffer _geometry = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly SsaoFramebuffer _ssao = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ForwardFramebuffer _forward = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly CombinedFramebuffer _combined = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly FxaaFramebuffer _fxaa = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly PickingFramebuffer _picking = new(DefaultWidthHeight, DefaultWidthHeight);
    private readonly ShadowFramebuffer _shadow = new(2048, 4);

    private bool _updateShadows = true;
    private Vector4 _cascadePlaneDistances = Vector4.One;
    private Matrix4x4[] _lightViewProjectionMatrices = [];

    private readonly float[] _cascadeDistances = [10f, 25f, 50f, 100f];

    public void Generate(int pairIndex)
    {
        Camera.PairIndex = pairIndex;
        TransformSystem.OnTransformComponentUpdated += _ =>
        {
            _updateShadows = true;
        };

        _geometry.Generate();
        _ssao.Generate();
        _forward.Generate();
        _combined.Generate();
        _fxaa.Generate();
        _picking.Generate();
        _shadow.Generate();
    }

    public void DeferredRendering(Action<CameraComponent, ActorSystemType> render, ClusteredLightSystem? lightSystem, DirectionalLightComponent? directionalLightComponent = null)
    {
        _geometry.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        GL.Disable(EnableCap.Blend);

        render(Camera, ActorSystemType.Deferred);

        if (Camera.bAmbientOcclusion)
        {
            _ssao.Bind();
            GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ssao.Render(shader =>
            {
                _geometry.BindTextures(true, true);
                shader.SetUniform("uProjectionMatrix", Camera.ProjectionMatrix);
                shader.SetUniform("radius", Camera.SsaoRadius);
            });
        }

        _geometry.Render(shader =>
        {
            Matrix4x4.Invert(Camera.ViewMatrix, out var inverseViewMatrix);
            shader.SetUniform("uInverseViewMatrix", inverseViewMatrix);
            shader.SetUniform("uZNear", Camera.NearPlaneDistance);
            shader.SetUniform("uZFar", Camera.FarPlaneDistance);

            if (directionalLightComponent is { Actor.IsVisible: true })
            {
                Matrix4x4.Decompose(directionalLightComponent.WorldMatrix, out _, out var rotation, out _);

                shader.SetUniform("useSunLight", true);
                shader.SetUniform("uSunDirection", Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, rotation)));
                shader.SetUniform("uSunColor", directionalLightComponent.Color);
                shader.SetUniform("uSunIntensity", directionalLightComponent.Intensity);

                shader.SetUniform("useShadows", Camera.bShadows);
                if (Camera.bShadows)
                {
                    shader.SetUniform("uCascadeCount", _shadow.CascadeCount);
                    shader.SetUniform("uCascadePlaneDistances", _cascadePlaneDistances);
                    shader.SetUniform("uLightViewProjectionMatrices", _lightViewProjectionMatrices);

                    _shadow.Bind(5);
                    shader.SetUniform("shadowMap", 5);
                }
            }
            else shader.SetUniform("useSunLight", false);

            shader.SetUniform("useSsao", Camera.bAmbientOcclusion);
            if (Camera.bAmbientOcclusion)
            {
                _ssao.Bind(4);
                shader.SetUniform("ssao", 4);
            }

            if (lightSystem is { IsEnabled: true })
            {
                lightSystem.BindForRendering();

                shader.SetUniform("useLighting", true);
                shader.SetUniform("uGridDimX", lightSystem.GridDimensionX);
                shader.SetUniform("uGridDimY", lightSystem.GridDimensionY);
                shader.SetUniform("uGridDimZ", lightSystem.GridDimensionZ);
            }
            else shader.SetUniform("useLighting", false);
        });
    }

    public void ForwardRendering(Action<CameraComponent, ActorSystemType> render)
    {
        // copy depth from gBuffer
        GL.BlitNamedFramebuffer(_geometry, _forward, 0, 0, _geometry.Width, _geometry.Height, 0, 0, _forward.Width, _forward.Height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

        _forward.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Enable(EnableCap.Blend);

        render(Camera, ActorSystemType.Forward);
    }

    public void PickingRendering()
    {
        _picking.Bind();
        _geometry.BindPicking(0);
        _forward.BindPicking(1);
        _picking.Render();
    }

    public void ShadowRendering(Action<CameraComponent[]> render, DirectionalLightComponent? directionalLightComponent = null)
    {
        if (!Camera.bShadows || !_updateShadows || directionalLightComponent is not { Actor.IsVisible: true }) return;

        _shadow.Bind();
        GL.Clear(ClearBufferMask.DepthBufferBit);

        Matrix4x4.Decompose(directionalLightComponent.WorldMatrix, out _, out var rotation, out _);
        var lightDir = Vector3.Transform(Vector3.UnitZ, rotation);

        var near = Camera.NearPlaneDistance;
        var far = MathF.Min(1000.0f, Camera.FarPlaneDistance); // Cap at 1000 units
        var aspect = Camera.AspectRatio;
        var fov = Camera.FieldOfViewRadians;

        // Initialize arrays for cascades
        var cascadeCount = _shadow.CascadeCount;
        _lightViewProjectionMatrices = new Matrix4x4[cascadeCount];
        var shadowCameras = new CameraComponent[cascadeCount];

        // Use frustum-based cascade splits with fixed distance ratios
        // This adapts to the camera's frustum while maintaining good quality distribution
        float clipRange = far - near;
        for (int i = 0; i < cascadeCount; i++)
        {
            // Use the predefined distances as ratios if they fit within the frustum
            float targetDistance = _cascadeDistances[i];

            // If target distance exceeds frustum far plane, scale proportionally
            if (targetDistance > far)
            {
                // Scale down to fit within frustum
                float ratio = targetDistance / _cascadeDistances[^1];
                _cascadePlaneDistances[i] = near + clipRange * ratio;
            }
            else
            {
                _cascadePlaneDistances[i] = targetDistance;
            }
        }

        Matrix4x4.Invert(Camera.ViewMatrix, out var invView);

        // Create shadow map for each cascade
        for (int cascadeIndex = 0; cascadeIndex < cascadeCount; cascadeIndex++)
        {
            var cascadeNear = cascadeIndex == 0 ? near : _cascadePlaneDistances[cascadeIndex - 1];
            var cascadeFar = _cascadePlaneDistances[cascadeIndex];

            // Calculate frustum corners for this cascade
            var nearHeight = 2.0f * MathF.Tan(fov / 2.0f) * cascadeNear;
            var nearWidth = nearHeight * aspect;
            var farHeight = 2.0f * MathF.Tan(fov / 2.0f) * cascadeFar;
            var farWidth = farHeight * aspect;

            Vector3[] frustumCorners =
            [
                // near plane
                new Vector3(-nearWidth / 2,  nearHeight / 2, -cascadeNear),
                new Vector3( nearWidth / 2,  nearHeight / 2, -cascadeNear),
                new Vector3( nearWidth / 2, -nearHeight / 2, -cascadeNear),
                new Vector3(-nearWidth / 2, -nearHeight / 2, -cascadeNear),
                // far plane
                new Vector3(-farWidth / 2,  farHeight / 2, -cascadeFar),
                new Vector3( farWidth / 2,  farHeight / 2, -cascadeFar),
                new Vector3( farWidth / 2, -farHeight / 2, -cascadeFar),
                new Vector3(-farWidth / 2, -farHeight / 2, -cascadeFar),
            ];

            // Transform frustum corners to world space
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                frustumCorners[i] = Vector3.Transform(frustumCorners[i], invView);
            }

            // Calculate the center of the frustum slice
            Vector3 center = Vector3.Zero;
            foreach (var corner in frustumCorners)
            {
                center += corner;
            }
            center /= frustumCorners.Length;

            // Position light to cover this cascade
            float shadowDistance = (cascadeFar - cascadeNear) / 2.0f + cascadeNear;
            var lightPos = center - lightDir * shadowDistance;
            var viewMatrix = Matrix4x4.CreateLookAt(lightPos, center, Vector3.UnitY);

            // Transform frustum corners to light space
            Vector3[] lightSpaceCorners = new Vector3[frustumCorners.Length];
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                lightSpaceCorners[i] = Vector3.Transform(frustumCorners[i], viewMatrix);
            }

            // Find the AABB in light space
            Vector3 min = lightSpaceCorners[0];
            Vector3 max = lightSpaceCorners[0];
            foreach (var corner in lightSpaceCorners)
            {
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            // Extend the depth range to include objects behind and in front of the frustum
            // This prevents shadow casters outside the frustum from being clipped
            float zExtension = (max.Z - min.Z) * 0.5f;
            min.Z -= zExtension;
            max.Z += zExtension;

            // Reduce padding for near cascades to maximize resolution where it matters most
            float paddingScale = cascadeIndex switch
            {
                0 => 0.02f,
                1 => 0.05f,
                _ => 0.1f
            };
            var padding = (max - min) * paddingScale;
            min.X -= padding.X;
            max.X += padding.X;
            min.Y -= padding.Y;
            max.Y += padding.Y;

            // Stabilize shadow map to prevent shimmering/swimming artifacts when camera moves
            // Round the extents to texel-sized increments
            float worldUnitsPerTexel = (max.X - min.X) / _shadow.Width;
            min.X = MathF.Floor(min.X / worldUnitsPerTexel) * worldUnitsPerTexel;
            min.Y = MathF.Floor(min.Y / worldUnitsPerTexel) * worldUnitsPerTexel;
            max.X = MathF.Floor(max.X / worldUnitsPerTexel) * worldUnitsPerTexel;
            max.Y = MathF.Floor(max.Y / worldUnitsPerTexel) * worldUnitsPerTexel;

            // Create orthographic projection for this cascade
            var projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                min.X, max.X,
                min.Y, max.Y,
                -max.Z, -min.Z);  // Note: negated because we're looking down -Z in view space

            // Store the light view projection matrix for this cascade
            _lightViewProjectionMatrices[cascadeIndex] = viewMatrix * projectionMatrix;

            // Create shadow camera for rendering
            shadowCameras[cascadeIndex] = new CameraComponent
            {
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                ViewProjectionMatrix = _lightViewProjectionMatrices[cascadeIndex],
            };
        }

        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Front);

        render(shadowCameras);

        GL.CullFace(TriangleFace.Back);
        GL.Disable(EnableCap.CullFace);

        _updateShadows = false;
    }

    public void CombineRendering()
    {
        _combined.Bind();
        GL.ClearColor(0.2f, 0.2f, 0.2f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _combined.Render(_ =>
        {
            _geometry.Bind(0);
            _forward.Bind(1);
            _picking.Bind(2);
        });
    }

    public void ApplyFxaa()
    {
        if (!Camera.bFXAA) return;

        _fxaa.Bind();
        GL.ClearColor(0, 0, 0, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _fxaa.Render(_ => _combined.Bind(0));
    }

    public void RenderToScreen(int width, int height)
    {
        FullQuadFramebuffer framebuffer = Camera.bFXAA ? _fxaa : _combined;
        GL.BlitNamedFramebuffer(framebuffer, 0, 0, 0, framebuffer.Width, framebuffer.Height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
    }

    public uint ReadPickingPixel(Vector2 mousePos, Vector2 windowPos) => _picking.ReadPixel(mousePos, windowPos, Camera.ViewportSize);
    public void SetPickedIds(IEnumerable<uint> ids) => _picking.SetPickedIds(ids);

    public void Resize(int newWidth, int newHeight)
    {
        _geometry.Resize(newWidth, newHeight);
        _ssao.Resize(newWidth, newHeight);
        _forward.Resize(newWidth, newHeight);
        _combined.Resize(newWidth, newHeight);
        _fxaa.Resize(newWidth, newHeight);
        _picking.Resize(newWidth, newHeight);
    }

    public Texture[] GetTextures() =>
    [
        .._geometry.GetTextures(),
        .._ssao.GetTextures(),
        .._forward.GetTextures(),
        .._shadow.GetTextures(),
        ..Camera.bFXAA ? _fxaa.GetTextures() : _combined.GetTextures(),
    ];

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _geometry.Allocated;
            total += _ssao.Allocated;
            total += _forward.Allocated;
            total += _combined.Allocated;
            total += _fxaa.Allocated;
            total += _picking.Allocated;
            total += _shadow.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _geometry.Used;
            total += _ssao.Used;
            total += _forward.Used;
            total += _combined.Used;
            total += _fxaa.Used;
            total += _picking.Used;
            total += _shadow.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("GBuffer", _geometry);
        yield return new MemoryDetail("SSAO", _ssao);
        yield return new MemoryDetail("Forward", _forward);
        yield return new MemoryDetail("Combined", _combined);
        yield return new MemoryDetail("FXAA", _fxaa);
        yield return new MemoryDetail("Picking", _picking);
        yield return new MemoryDetail("Shadow", _shadow);
    }
}
