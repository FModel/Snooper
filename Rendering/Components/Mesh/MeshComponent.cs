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
    public abstract int LODCount { get; }

    public int LODIndex { get; private set; }
    public float[] ScreenSizes { get; protected init; } = [];
    public float CurrentScreenSize = 0;

    // public override void Update(DrawIndirectBuffer commands, ElementArrayBuffer<uint> ebo, ArrayBuffer<Vertex> vbo)
    // {
    //     if (LODCount < 2 || ScreenSizes.Length < 2 ||
    //         Actor is not MeshActor { ActorManager: SceneSystem { ActiveCamera: {} camera }, IsVisible: true } actor)
    //         return;
    //
    //     CurrentScreenSize = actor.CullingComponent.GetScreenSpaceCoverage(camera);
    //
    //     var currentLODIndex = LODIndex;
    //     for (var i = 0; i < ScreenSizes.Length; i++)
    //     {
    //         if (CurrentScreenSize >= ScreenSizes[i])
    //         {
    //             currentLODIndex = i;
    //             break;
    //         }
    //     }
    //
    //     if (currentLODIndex != LODIndex && currentLODIndex >= 0 && currentLODIndex < LODCount)
    //     {
    //         Console.WriteLine("{0}: Screen Size: {1}, Switching LOD from {2} to {3}", actor.Name, CurrentScreenSize, LODIndex, currentLODIndex);
    //         LODIndex = currentLODIndex;
    //
    //         var primitive = GetPrimitive(LODIndex);
    //
    //         base.Update(commands, ebo, vbo);
    //         vbo.Update(primitive.Vertices);
    //
    //         // if (GL.GetInteger(GetPName.VertexArrayBinding) != VAO) VAO.Bind();
    //         ebo.Update(primitive.Indices);
    //     }
    // }

    protected abstract IVertexData GetPrimitive(int index);
}
