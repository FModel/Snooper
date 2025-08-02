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
    protected virtual bool IsRenderable => true;
    protected virtual bool CullingEnabled => true;
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

        // this trigger a shader use, do it before pre-rendering to avoid conflicts
        if (CullingEnabled)
            Resources.Cull(camera);
        
        PreRender(camera);
        base.Render(camera);
        PostRender(camera);
    }

    protected virtual void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        
    }

    protected override void OnActorComponentEnqueued(TComponent component)
    {
        base.OnActorComponentEnqueued(component);
        if (this is DebugSystem or SkyboxSystem or GridSystem) return;

        Vector3? color = null;
        if (component.Actor?.Parent is { Parent: not null }) // just an example
        {
            var id = component.Actor.Parent.Id;
            color = new Vector3((id & 0xFF) / 255f, ((id >> 8) & 0xFF) / 255f, ((id >> 16) & 0xFF) / 255f);
        }
        
        component.Actor?.Components.Add(new DebugComponent(component.Bounds, color));
    }
}

public class PrimitiveSystem<TComponent, TInstanceData, TPerDrawData>(int initialDrawCapacity)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerDrawData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    protected override Action<ArrayBuffer<Vector3>> PointersFactory { get; } = buffer =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, buffer.Stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem<TComponent>(int initialDrawCapacity)
    : PrimitiveSystem<TComponent, PerInstanceData, PerDrawData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerDrawData>
{
    protected override bool CullingEnabled => false; // disable culling for grid, skybox, and default primitives
}
public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>(10);
