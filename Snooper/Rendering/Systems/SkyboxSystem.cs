using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Skybox;

namespace Snooper.Rendering.Systems;

public class SkyboxSystem() : PrimitiveSystem<CubeComponent>(1)
{
    public override uint Order => 1;
    protected override bool AllowDerivation => true;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("Skybox/skybox");

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        var view = camera.ViewMatrix;
        view.M41 = 0;
        view.M42 = 0;
        view.M43 = 0;
        
        Shader.Use();
        Shader.SetUniform("uViewMatrix", view);
        Shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);

        switch (_component)
        {
            case AtmosphericComponent atmospheric:
            {
                Shader.SetUniform("uSunPos", atmospheric.Sun.Position);
                Shader.SetUniform("uSunIntensity", atmospheric.Sun.Intensity);
                Shader.SetUniform("uSunRadius", atmospheric.Sun.Radius);
                Shader.SetUniform("uSunAtmosphereRadius", atmospheric.Sun.AtmosphereRadius);
                break;
            }
        }
        
        GL.DepthFunc(DepthFunction.Lequal);
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    protected override void OnActorComponentAdded(CubeComponent component)
    {
        base.OnActorComponentAdded(component);
        
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