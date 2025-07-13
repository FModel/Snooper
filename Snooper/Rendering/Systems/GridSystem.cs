using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public class GridSystem() : PrimitiveSystem<GridComponent>(1)
{
    public override uint Order => 2;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("grid");

    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
        
        Shader.SetUniform("uNear", camera.NearPlaneDistance);
        Shader.SetUniform("uFar", camera.FarPlaneDistance);
        
        GL.DepthMask(false);
    }
    
    protected override void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        GL.DepthMask(true);
    }
}
