using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Programs;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;

namespace Snooper.Rendering.Systems;

public abstract class PrimitiveSystem<TVertex, TComponent, TInstanceData, TPerDrawData>(int initialDrawCapacity, PrimitiveType type = PrimitiveType.Triangles)
    : IndirectRenderSystem<TVertex, TComponent, TInstanceData, TPerDrawData>(initialDrawCapacity, type), IPickableSystem
    where TVertex : unmanaged
    where TComponent : PrimitiveComponent<TVertex, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    public override uint Order => 20;
    protected override bool AllowDerivation => false;
    protected virtual bool IsRenderable => true;
    protected virtual bool IsCulled => true;
    protected virtual bool IsPickable => true;
    protected virtual ShaderProgram Shader { get; } = new EmbeddedShaderProgram("default");
    
    private ShaderProgram? _picking;

    public override void Load()
    {
        base.Load();

        Shader.Generate();
        Shader.Link();

        if (IsPickable)
        {
            _picking = new EmbeddedShaderProgram(Shader.Vertex, "picking.frag")
            {
                TessellationControl = Shader.TessellationControl,
                TessellationEvaluation = Shader.TessellationEvaluation
            };
            _picking.Generate();
            _picking.Link();
        }
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
        if (IsCulled)
            Resources.Cull(camera);
        
        PreRender(camera);
        base.Render(camera);
        PostRender(camera);
    }

    protected virtual void PostRender(CameraComponent camera, int batchIndex = 0)
    {
        
    }
    
    protected virtual void PreRenderPicking(CameraComponent camera, ShaderProgram shader)
    {
        shader.Use();
        shader.SetUniform("uViewMatrix", camera.ViewMatrix);
        shader.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
    }
    
    public void RenderPicking(CameraComponent camera)
    {
        if (!IsRenderable || !IsPickable) return;
        
        if (_picking is null)
            throw new InvalidOperationException("Picking shader is not initialized.");

        PreRenderPicking(camera, _picking);
        base.Render(camera);
        PostRenderPicking(camera, _picking);
    }
    
    protected virtual void PostRenderPicking(CameraComponent camera, ShaderProgram shader)
    {
        
    }
}

public class PrimitiveSystem<TComponent, TInstanceData, TPerDrawData>(int initialDrawCapacity)
    : PrimitiveSystem<Vector3, TComponent, TInstanceData, TPerDrawData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, TInstanceData, TPerDrawData>
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerDrawData : unmanaged, IPerDrawData
{
    protected override Action<int> VertexLayout { get; } = stride =>
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
    };
}

public class PrimitiveSystem<TComponent>(int initialDrawCapacity)
    : PrimitiveSystem<TComponent, PerInstanceData, PerDrawData>(initialDrawCapacity)
    where TComponent : PrimitiveComponent<Vector3, PerInstanceData, PerDrawData>
{
    protected override bool IsCulled => false; // disable culling for grid, skybox, and default primitives
    protected override bool IsPickable => false;
}
public class PrimitiveSystem() : PrimitiveSystem<PrimitiveComponent>(10);
