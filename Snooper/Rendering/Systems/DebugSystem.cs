using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;

namespace Snooper.Rendering.Systems;

public class DebugSystem() : PrimitiveSystem<DebugComponent, PerInstanceData, PerDrawDebugData>(500, PrimitiveType.Lines)
{
    public override uint Order => 100;
    protected override bool AllowDerivation => true;
    protected override bool IsCulled => false;
    protected override ShaderProgram Shader { get; } = new EmbeddedShaderProgram("default.vert", "debug.frag");
}
