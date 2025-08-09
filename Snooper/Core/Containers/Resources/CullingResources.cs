using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class CullingResources(int initialDrawCapacity) : IDisposable
{
    private readonly ShaderStorageBuffer<PrimitiveDescriptor> _primitives = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<PrimitiveSectionDescriptor> _sections = new(initialDrawCapacity);
    private readonly ShaderProgram _compute = new EmbeddedShaderProgram(string.Empty, string.Empty)
    {
        Compute = "culling.comp"
    };
    
    public void Generate()
    {
        _primitives.Generate();
        _sections.Generate();
        
        _compute.Generate();
        _compute.Link();
    }
    
    public void Allocate(int componentCount, int drawCount)
    {
        _primitives.Bind();
        _primitives.Allocate(new PrimitiveDescriptor[componentCount]);
        _primitives.Unbind();
        
        _sections.Bind();
        _sections.Allocate(new PrimitiveSectionDescriptor[drawCount]);
        _sections.Unbind();
    }
    
    public int Add(PrimitiveSectionDescriptor[] sections)
    {
        _sections.Bind();
        var sectionOffset = _sections.AddRange(sections);
        _sections.Unbind();
        
        return sectionOffset;
    }

    public int Add(PrimitiveDescriptor descriptor)
    {
        _primitives.Bind();
        var modelId = _primitives.Add(descriptor);
        _primitives.Unbind();
        
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
        _compute.SetUniform("uProjectionMatrix", camera.ProjectionMatrix);
        _compute.SetUniform("uCameraPosition", camera.Actor.Transform.Position);
        
        instances.Bind(0);
        _primitives.Bind(1);
        _sections.Bind(2);
        commands.Bind(3);

        const int groupSize = 64;
        var dispatchCount = (commands.Count + groupSize - 1) / groupSize;
        GL.DispatchCompute(dispatchCount, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit);
    }

    public void Remove(int index)
    {
        // _primitives.Bind();
        // _primitives.Remove();
        // _primitives.Unbind();
        //
        // _sections.Bind();
        // _sections.Remove();
        // _sections.Unbind();
    }

    public void Dispose()
    {
        _primitives.Dispose();
        _sections.Dispose();
        _compute.Dispose();
    }
}