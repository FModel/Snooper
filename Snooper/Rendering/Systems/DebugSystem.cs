using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Camera;
using System.Numerics;

namespace Snooper.Rendering.Systems;

public class DebugSystem() : PrimitiveSystem<DebugComponent, PerInstanceData, PerMaterialDebugData>(PrimitiveType.Lines)
{
    public override uint Order => 50;
    protected override bool AllowDerivation => true;
    protected override bool IsCulled => false;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("default.vert", "debug.frag")
    {
        Geometry = "debug.geom",
        Defines = ["USE_GEOMETRY_SHADER"]
    };

    protected override void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        base.PreRender(camera, shader);

        // Pass viewport size for line thickness calculations in geometry shader
        shader.SetUniform("uViewportSize", new Vector2(camera.ViewportSize.X, camera.ViewportSize.Y));
    }
}
