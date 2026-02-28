using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Skybox;

namespace Snooper.Rendering.Systems;

public class SkyboxSystem : PrimitiveSystem<CubeComponent>
{
    public override uint Order => 1;
    protected override bool AllowDerivation => true;
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("Skybox/skybox")
    };

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        var view = camera.ViewMatrix;
        view.M41 = 0;
        view.M42 = 0;
        view.M43 = 0;

        shader.Use();
        shader.SetUniform("uViewMatrix", view);
        shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);

        switch (_component)
        {
            case AtmosphericComponent atmospheric:
            {
                shader.SetUniform("uSunPos", atmospheric.Sun.Position);
                shader.SetUniform("uSunIntensity", atmospheric.Sun.Intensity);
                shader.SetUniform("uSunRadius", atmospheric.Sun.Radius);
                shader.SetUniform("uSunAtmosphereRadius", atmospheric.Sun.AtmosphereRadius);
                break;
            }
        }

        GL.DepthFunc(DepthFunction.Lequal);
        GL.DepthMask(false);
    }

    protected override void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    protected override void OnActorComponentEnqueued(CubeComponent component)
    {
        base.OnActorComponentEnqueued(component);

        if (_component is not null)
            throw new InvalidOperationException("Only one SkyboxComponent can be added to the system at a time.");

        _component = component;
    }

    protected override void OnActorComponentRemoved(CubeComponent component)
    {
        base.OnActorComponentRemoved(component);

        if (_component == component)
        {
            _component = null;
        }
    }

    private CubeComponent? _component;
}
