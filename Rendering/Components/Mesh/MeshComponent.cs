using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(RenderSystem))]
public class MeshComponent(IPrimitiveData primitive) : PrimitiveComponent(primitive)
{
    public int LODIndex { get; protected set; }

    public override void Generate()
    {
        VBO.UsageHint = BufferUsageHint.DynamicDraw;
        EBO.UsageHint = BufferUsageHint.DynamicDraw;

        base.Generate();
    }
}
