using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Primitives;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(RenderSystem))]
public abstract class MeshComponent(IPrimitiveData primitive) : PrimitiveComponent(primitive)
{
    public int LODIndex { get; private set; }
    public float[] ScreenSizes { get; protected init; } = [];

    public override void Generate()
    {
        VBO.UsageHint = BufferUsageHint.DynamicDraw;
        EBO.UsageHint = BufferUsageHint.DynamicDraw;

        base.Generate();
    }

    public override void Update()
    {
        if (ScreenSizes.Length < 2 || Actor is not MeshActor { ActorManager: SceneSystem { ActiveCamera: {} camera } } actor)
            return;

        var screenSize = actor.SphereCullingComponent.GetScreenSpaceCoverage(camera);

        var currentLODIndex = LODIndex;
        for (var i = 0; i < ScreenSizes.Length; i++)
        {
            if (screenSize >= ScreenSizes[i])
            {
                currentLODIndex = i;
                break;
            }
        }

        if (currentLODIndex != LODIndex)
        {
            Console.WriteLine("{0}: Switching LOD from {1} to {2}", actor.Name, LODIndex, currentLODIndex);
            LODIndex = currentLODIndex;
            
            var primitive = GetPrimitive(LODIndex);
            VBO.Bind();
            VBO.Update(primitive.Vertices);

            EBO.Bind();
            EBO.Update(primitive.Indices);
        }
    }

    public abstract IPrimitiveData GetPrimitive(int index);
}
