using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData>(initialDrawCapacity, type)
    where TVertex : unmanaged
    where TComponent : TPrimitiveComponent<TVertex, TInstanceData>
    where TInstanceData : unmanaged, IPerInstanceData
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;
    protected abstract int BatchCount { get; }
    protected virtual ShaderProgram Shader { get; } = new EmbeddedShaderProgram("default");

    public override void Load()
    {
        base.Load();

        Shader.Generate();
        Shader.Link();
    }

    protected virtual void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        Shader.Use();
        Shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        Shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
    }

    public sealed override void Render(CameraComponent camera)
    {
        if (!IsRenderable) return;

        for (var batchIndex = 0; batchIndex < Resources.Count; batchIndex += BatchCount)
        {
            PreRender(camera, batchIndex);
            Resources.RenderBatch(batchIndex, BatchCount);
            PostRender(camera, batchIndex);
        }
    }

    protected virtual void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        
    }
}

public class PrimitiveSystem<TComponent>(int initialDrawCapacity) : PrimitiveSystem<Vector3, TComponent, PerInstanceData>(initialDrawCapacity) where TComponent : TPrimitiveComponent<Vector3, PerInstanceData>
{
    protected override int BatchCount => int.MaxValue;
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>(10);
