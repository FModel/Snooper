using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class CullingResources(int initialDrawCapacity) : IDisposable
{
    private readonly ShaderStorageBuffer<PrimitiveOffsets> _primitives = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<SectionDescriptor> _sections = new(initialDrawCapacity);
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
    
    public void Allocate(uint componentCount, uint drawCount)
    {
        _primitives.Bind();
        _primitives.Allocate(new PrimitiveOffsets[componentCount]);
        _primitives.Unbind();
        
        _sections.Bind();
        _sections.Allocate(new SectionDescriptor[drawCount]);
        _sections.Unbind();
    }
    
    public int Add(SectionDescriptor[] sections)
    {
        _sections.Bind();
        var sectionOffset = _sections.AddRange(sections);
        _sections.Unbind();
        
        return sectionOffset;
    }

    public int Add(PrimitiveOffsets offsets)
    {
        _primitives.Bind();
        var modelId = _primitives.Add(offsets);
        _primitives.Unbind();
        
        return modelId;
    }
    
    public void UpdateOverrideLod(int modelId, int overrideLod)
    {
        _primitives.Bind();
        GL.BufferSubData(BufferTarget.ShaderStorageBuffer, modelId * _primitives.Stride + 32, 4, ref overrideLod);
        _primitives.Unbind();
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
        _compute.SetUniform("uCameraPosition", camera.LocalTransform.Position);

        commands.Bind(0);
        instances.Bind(1);
        _primitives.Bind(2);
        _sections.Bind(3);

        GL.DispatchCompute(commands.Count, 1, 1);
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