using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type = PrimitiveType.Triangles)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerMaterialData>(type)
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
    
    protected override void OnLoad()
    {
        base.OnLoad();

        Shader.Generate();
        Shader.Link();
    }

    protected override void OnUpdate(float delta)
    {
        if (!IsRenderable) return;
        base.OnUpdate(delta);
    }

    protected virtual void PreRender(CameraComponent camera, ShaderProgram shader)
    {
        shader.Use();
        shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
    }

    protected sealed override void OnRender(CameraComponent camera)
    {
        if (!IsRenderable) return;

        // this trigger a shader use, do it before pre-rendering to avoid conflicts
        if (IsCulled)
            Resources.Cull(camera);
        
        PreRender(camera, Shader);
        base.OnRender(camera);
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

public class PrimitiveSystem<TComponent, TInstanceData, TPerMaterialData>(PrimitiveType type = PrimitiveType.Triangles)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerMaterialData>(type)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerMaterialData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    protected override Action<uint> VertexLayout { get; } = vao =>
    {
        GL.VertexArrayAttribFormat(vao, 0, 3, VertexAttribType.Float, false, 0);
        GL.EnableVertexArrayAttrib(vao, 0);
        GL.VertexArrayAttribBinding(vao, 0, 0);
    };
}

public class PrimitiveSystem<TComponent>
    : PrimitiveSystem<TComponent, PerInstanceData, PerMaterialData>
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerMaterialData>
{
    protected override bool IsCulled => false; // disable culling for grid, skybox, and default primitives
}
public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>;
