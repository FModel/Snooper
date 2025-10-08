using Snooper.Core.Containers.Buffers;
using Snooper.Core.Systems;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Mesh;

namespace Snooper.Rendering.Systems;

public class SplineRenderSystem : DeferredRenderSystem
{
    public override uint Order => 24;
    public override ActorSystemType SystemType => ActorSystemType.Deferred;
    
    private readonly ShaderStorageBuffer<SplineMeshParams> _params = new(100);
    
    public override void Load()
    {
        Shader.Vertex = "spline.vert";
        
        base.Load();
        
        _params.Generate();
        _params.Bind();
        foreach (var component in Components.Cast<SplineMeshComponent>())
        {
            _params.Add(component.SplineParams);
        }
        _params.Unbind();
    }
    
    protected override void PreRender(CameraComponent camera, int batchIndex = 0)
    {
        base.PreRender(camera, batchIndex);
    
        _params.Bind(3);
    }

    public override bool Accepts(Type type) => type == typeof(SplineMeshComponent);

    protected override bool CanEnqueueActorComponent(MeshComponent component) => true;
}