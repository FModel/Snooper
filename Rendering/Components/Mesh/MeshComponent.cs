using OpenTK.Graphics.OpenGL4;
using Snooper.Core;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Systems;

namespace Snooper.Rendering.Components.Mesh;

[DefaultActorSystem(typeof(DeferredRenderSystem))]
public abstract class MeshComponent(IVertexData primitive) : TPrimitiveComponent<Vertex>(primitive)
{
    protected override Action<ArrayBuffer<Vertex>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, buffer.Stride, 12);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, buffer.Stride, 24);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, buffer.Stride, 36);
        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);
        GL.EnableVertexAttribArray(3);
    };
    protected override PolygonMode PolygonMode { get => PolygonMode.Fill; }

    public abstract int LODCount { get; }

    public int LODIndex { get; private set; }
    public float[] ScreenSizes { get; protected init; } = [];
    public float CurrentScreenSize = 0;

    public override void Generate()
    {
        VBO.UsageHint = BufferUsageHint.DynamicDraw;
        EBO.UsageHint = BufferUsageHint.DynamicDraw;

        base.Generate();
    }

    public override void Update()
    {
        if (LODCount < 2 || ScreenSizes.Length < 2 ||
            Actor is not MeshActor { ActorManager: SceneSystem { ActiveCamera: {} camera }, IsVisible: true } actor)
            return;

        CurrentScreenSize = actor.CullingComponent.GetScreenSpaceCoverage(camera);

        var currentLODIndex = LODIndex;
        for (var i = 0; i < ScreenSizes.Length; i++)
        {
            if (CurrentScreenSize >= ScreenSizes[i])
            {
                currentLODIndex = i;
                break;
            }
        }

        if (currentLODIndex != LODIndex && currentLODIndex >= 0 && currentLODIndex < LODCount)
        {
            Console.WriteLine("{0}: Screen Size: {1}, Switching LOD from {2} to {3}", actor.Name, CurrentScreenSize, LODIndex, currentLODIndex);
            LODIndex = currentLODIndex;

            var primitive = GetPrimitive(LODIndex);

            base.Update();
            VBO.Bind();
            VBO.Update(primitive.Vertices);

            EBO.Bind();
            EBO.Update(primitive.Indices);
        }
    }

    protected abstract IVertexData GetPrimitive(int index);
}
