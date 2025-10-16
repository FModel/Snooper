using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem() : PrimitiveSystem<GridComponent>(1)
{
    public override uint Order => 2;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("grid");

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);
        
        shader.SetUniform("uNear", camera.NearPlaneDistance);
        shader.SetUniform("uFar", camera.FarPlaneDistance);
        
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        GL.DepthMask(true);
    }
}
