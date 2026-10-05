using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Skybox;
using Snooper.Rendering.Containers.Framebuffers;

namespace Snooper.Rendering.Systems;

public sealed class SkyboxSystem : ActiveComponentSystem<SkyAtmosphereComponent>, IGeometryRenderSystem
{
    public override ActorSystemType SystemType => ActorSystemType.Environment;
    public override uint Order => 1;

    private readonly ShaderProgram _shader = new EmbeddedShader("Skybox/skybox");
    private readonly VertexArray _vao = new(); // the vertex shader builds the triangle, but a draw call still needs a vao bound
    private readonly SkyFramebuffer _sky = new();

    protected override void OnLoad()
    {
        base.OnLoad();

        _shader.Generate();
        _shader.Link();
        _vao.Generate();

        _sky.Generate();
    }

    public void Cull(ReadOnlySpan<CullView> views) { }
    public void RenderMask(CameraComponent camera) { }

    public void Render(CameraComponent camera, CommandBufferType type)
    {
        if (type != CommandBufferType.Transparent || Active is not { } atmosphere) return;

        using (Scope())
        using (Profiler.Sample(DisplayName))
        using (Profiler.Draw())
        {
            _shader.Use();
            _shader.SetUniform("uInverseProjectionMatrix", camera.InverseProjectionMatrix);

            if (ActorManager?.GetSystem<DirectionalLightSystem>() is { Active: { } sun })
            {
                _sky.Bake(atmosphere, sun.GetDirection(), sun.Color * sun.Daylight, camera.InverseViewMatrix.Translation.Y);

                _shader.SetUniform("useSun", true);
                _shader.SetUniform("uSunPos", _sky.Baked.SunDirection);
                _shader.SetUniform("uSunDisc", MathF.Cos(float.DegreesToRadians(sun.SourceAngle * 0.5f)));
                _shader.SetUniform("uSky", 0);
                _sky.Bind(ESkyTexture.Color, 0);
            }
            else _shader.SetUniform("useSun", false);

            GL.DepthFunc(DepthFunction.Gequal);
            GL.DepthMask(false);

            _vao.Bind();
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            _vao.Unbind();

            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Greater);
            _shader.Unuse();
        }
    }

    public override long Allocated => _shader.Allocated + _vao.Allocated + _sky.Allocated;
    public override long Used => _shader.Used + _vao.Used + _sky.Used;

    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Shader", _shader);
        yield return new MemoryDetail("Vertex Array", _vao);
        yield return new MemoryDetail("Sky Framebuffer", _sky);
    }

    public override void Dispose()
    {
        base.Dispose();

        _shader.Dispose();
        _vao.Dispose();
        _sky.Dispose();
    }
}
