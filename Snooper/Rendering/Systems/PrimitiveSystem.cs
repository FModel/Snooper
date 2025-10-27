using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(initialDrawCapacity, type)
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;
    protected virtual bool IsRenderable => true;
    protected virtual bool IsCulled => true;
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

    protected virtual void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        shader.Use();
        shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
    }

    public sealed override void Render(CameraComponent camera)
    {
        if (!IsRenderable) return;

        // this trigger a shader use, do it before pre-rendering to avoid conflicts
        if (IsCulled)
            Resources.Cull(camera);
        
        PreRender(camera, Shader);
        base.Render(camera);
        PostRender(camera, Shader);
    }

    protected virtual void PostRender(CameraComponent camera, ShaderProgram shader)
    {
        
    }

    public override long Allocated => base.Allocated + Shader.Allocated;
    public override long Used => base.Used + Shader.Used;
    public override IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        foreach (var detail in base.GetMemoryDetails())
            yield return detail;
        
        yield return new MemoryDetail("Main Shader", Shader);
    }
}

public class PrimitiveSystem<TComponent, TInstanceData, TPerMaterialData>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerMaterialData>(initialDrawCapacity, type)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected override Action<int> VertexLayout { get; } = stride =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem<TComponent>(int initialDrawCapacity)
    : PrimitiveSystem<TComponent, PerInstanceData, PerMaterialData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerMaterialData>
{
    protected override bool IsCulled => false; // disable culling for grid, skybox, and default primitives
}
public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>(10);
