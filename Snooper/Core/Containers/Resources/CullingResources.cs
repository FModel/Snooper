using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class CullingResources(int initialDrawCapacity)
{
    private readonly ShaderStorageBuffer<CullingBounds> _bounds = new(initialDrawCapacity);
    private readonly ShaderProgram _compute = new EmbeddedShaderProgram(string.Empty, string.Empty)
    {
        Compute = "culling.comp"
    };
    
    public void Generate()
    {
        _bounds.Generate();
        
        _compute.Generate();
        _compute.Link();
    }
    
    public void Allocate(int componentCount)
    {
        _bounds.Bind();
        _bounds.Allocate(new CullingBounds[componentCount]);
        _bounds.Unbind();
    }

    public int Add(CullingBounds bounds)
    {
        _bounds.Bind();
        var modelId = _bounds.Add(bounds);
        _bounds.Unbind();
        
        return modelId;
    }
    
    public void Cull<TInstanceData>(CameraComponent camera, ShaderStorageBuffer<TInstanceData> instances, DrawIndirectBuffer commands) where TInstanceData : unmanaged, IPerInstanceData
    {
        var frustum = camera.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }
        
        _compute.Use();
        _compute.SetUniform("uFrustumPlanes", frustum);
        
        instances.Bind(0);
        _bounds.Bind(1);
        commands.Bind(2);
        
        const int groupSize = 64;
        var dispatchCount = (commands.Count + groupSize - 1) / groupSize;
        GL.DispatchCompute(dispatchCount, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit);
    }
}