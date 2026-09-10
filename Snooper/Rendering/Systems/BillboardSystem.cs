using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public class BillboardSystem : PrimitiveSystem<Vector2, BillboardComponent, PerInstanceData, PerMaterialBillboardData>
{
    public override uint Order => 29;
    protected override bool AllowDerivation => true; // billboard is used by other components like audio and decal
    protected override Dictionary<CommandBufferType, ShaderProgram> Shaders { get; } = new()
    {
        [CommandBufferType.Transparent] = new EmbeddedShader("billboard")
    };

    protected override Action<VertexArrayLayout> VertexLayout { get; } = layout => layout.Float(0, 2);
}
