using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class CullingResources(int initialDrawCapacity)
{
    private readonly ShaderStorageBuffer<PrimitiveDescriptor> _descriptors = new(initialDrawCapacity);
    private readonly ShaderProgram _compute = new EmbeddedShaderProgram(string.Empty, string.Empty)
    {
        Compute = "culling.comp"
    };
    
    public void Generate()
    {
        _descriptors.Generate();
        
        _compute.Generate();
        _compute.Link();
    }
    
    public void Allocate(int componentCount)
    {
        _descriptors.Bind();
        _descriptors.Allocate(new PrimitiveDescriptor[componentCount]);
        _descriptors.Unbind();
    }

    public int Add(PrimitiveDescriptor descriptor)
    {
        _descriptors.Bind();
        var modelId = _descriptors.Add(descriptor);
        _descriptors.Unbind();
        
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
        _compute.SetUniform("uCameraPosition", camera.Actor.Transform.Position);
        
        instances.Bind(0);
        _descriptors.Bind(1);
        commands.Bind(2);
        
        const int groupSize = 64;
        var dispatchCount = (commands.Count + groupSize - 1) / groupSize;
        GL.DispatchCompute(dispatchCount, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit);
    }
}