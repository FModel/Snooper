using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData, TPerDrawData>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerDrawData>(initialDrawCapacity, type)
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;
    protected abstract int BatchCount { get; }
    protected virtual bool IsRenderable => true;
    protected virtual ShaderProgram Shader { get; } = new EmbeddedShaderProgram("default");

    public override void Load()
    {
        base.Load();

        Shader.Generate();
        Shader.Link();
    }

    public override void Update(float delta)
    {
        if (!IsRenderable) return;
        base.Update(delta);
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

public class PrimitiveSystem<TComponent, TInstanceData, TPerDrawData>(int initialDrawCapacity)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerDrawData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    protected override int BatchCount => int.MaxValue;
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem<TComponent>(int initialDrawCapacity) : PrimitiveSystem<TComponent, PerInstanceData, PerDrawData>(initialDrawCapacity) where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerDrawData>;
public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>(10);
