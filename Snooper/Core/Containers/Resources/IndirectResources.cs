using System.Text;
using CUE4Parse.UE4.Objects.Core.Math;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Primitives;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex, TInstanceData, TPerDrawData>(int initialDrawCapacity, PrimitiveType type)
    : IBind, IMemorySizeProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerDrawData : unmanaged, IPerDrawData
{
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TPerDrawData> _drawData = new(initialDrawCapacity);
    
    private readonly ShaderProgram _compute = new EmbeddedShaderProgram(string.Empty, string.Empty) { Compute = "culling.comp"};
    private readonly ShaderStorageBuffer<AABB> _bounds = new(initialDrawCapacity);

    private readonly VertexArray _vao = new();
    public readonly ElementArrayBuffer<uint> EBO = new(initialDrawCapacity * 200);
    public readonly ArrayBuffer<TVertex> VBO = new(initialDrawCapacity * 100);
    
    public int Count => _commands.Current.Count;

    public void Generate()
    {
        _commands.Generate();
        _instanceData.Generate();
        _drawData.Generate();
        
        _compute.Generate();
        _compute.Link();
        _bounds.Generate();
        
        _vao.Generate();
        EBO.Generate();
        VBO.Generate();
    }

    public void Bind()
    {
        _commands.Current.Bind();
        _instanceData.Bind();
        
        _vao.Bind();
        EBO.Bind();
        VBO.Bind();
    }
    
    public void Allocate(int drawCount, int indices, int vertices)
    {
        _drawData.Bind();
        _drawData.Allocate(new TPerDrawData[drawCount]);
        _drawData.Unbind(); // instance ssbo is rebound here
        
        _bounds.Bind();
        _bounds.Allocate(new AABB[drawCount]);
        _bounds.Unbind(); // instance ssbo is rebound here

        _commands.Current.Allocate(new DrawElementsIndirectCommand[drawCount]);
        _instanceData.Allocate(new TInstanceData[drawCount * 10]);
        
        EBO.Allocate(new uint[indices]);
        VBO.Allocate(new TVertex[vertices]);
    }

    public void Unbind()
    {
        _commands.Current.Unbind();
        _instanceData.Unbind();
        
        _vao.Unbind();
        EBO.Unbind();
        VBO.Unbind();
    }
    
    public void Add(TPrimitiveData<TVertex> primitive, PrimitiveSection[] sections, TInstanceData[] instanceData, AABB bounding)
    {
        var firstIndex = EBO.AddRange(primitive.Indices);
        var baseVertex = VBO.AddRange(primitive.Vertices);
        var baseInstance = _instanceData.AddRange(instanceData);
        
        _bounds.Bind();
        _bounds.Add(bounding);
        _bounds.Unbind();

        foreach (var section in sections)
        {
            section.DrawMetadata.BaseInstance = baseInstance;
            section.DrawMetadata.DrawId = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = (uint)section.IndexCount,
                InstanceCount = (uint)instanceData.Length,
                FirstIndex = (uint)(firstIndex + section.FirstIndex),
                BaseVertex = (uint)baseVertex,
                BaseInstance = (uint)baseInstance
            });
        }
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> component)
    {
        if (!component.Actor.IsDirty || component.Sections.Length < 1) return;
        
        var metadata = component.Sections[0].DrawMetadata;
        // var visibleInstanceCount = component.Actor.VisibleInstances.End.Value - component.Actor.VisibleInstances.Start.Value;
        // var visibleBaseInstance = metadata.BaseInstance + component.Actor.VisibleInstances.Start.Value;
        
        // var instanceCount = component.Actor.IsVisible ? visibleInstanceCount : 0;
        // foreach (var section in component.Sections)
        // {
        //     _commands.Current.UpdateInstance(section.DrawMetadata.DrawId, (uint)instanceCount, (uint)visibleBaseInstance);
        // }
        
        _instanceData.Update(metadata.BaseInstance, component.GetPerInstanceData());
        component.Actor.MarkClean();
    }
    
    public void Update(int drawId, TPerDrawData drawData)
    {
        if (!drawData.IsReady) throw new InvalidOperationException("Draw data is not ready.");
        Log.Debug("Updating draw data for draw ID {DrawId}.", drawId);

        _drawData.Bind();
        _drawData.Update(drawId, drawData);
        _drawData.Unbind();
    }

    public void Update(int drawId, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        VBO.Update((int) command.BaseVertex, vertices);
    }

    public void Update(int drawId, uint[] indices, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        EBO.Update((int) command.FirstIndex, indices);
        VBO.Update((int) command.BaseVertex, vertices);

        _commands.Current.UpdateIndexCount(drawId, (uint) indices.Length);
    }

    public void Remove(IndirectDrawMetadata metadata)
    {
        _commands.Current.Bind();
        _commands.Current.Remove(metadata.DrawId);
        _commands.Current.Unbind();

        _instanceData.Bind();
        _instanceData.Remove(metadata.BaseInstance);
        _instanceData.Unbind();

        _drawData.Bind();
        _drawData.Remove(metadata.DrawId);
        _drawData.Unbind();
    }

    public void Cull(CameraComponent camera)
    {
        var frustum = camera.GetWorldFrustumPlanes();
        if (frustum.Length != 6)
        {
            throw new ArgumentException("Frustum must be defined by exactly six planes.");
        }
        
        _compute.Use();
        _compute.SetUniform("uFrustumPlanes", frustum);
        
        _instanceData.Bind(0);
        _bounds.Bind(1);
        _commands.Current.Bind(2);
        
        const int groupSize = 64;
        var dispatchCount = (Count + groupSize - 1) / groupSize;
        GL.DispatchCompute(dispatchCount, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit | MemoryBarrierFlags.ShaderStorageBarrierBit);
    }

    public void Render() => RenderBatch(IntPtr.Zero, Count);
    public void RenderBatch(nint offset, int batchSize)
    {
        _commands.Current.Bind();
        _instanceData.Bind(0);
        _drawData.Bind(1);
        _vao.Bind();

        var batchCount = Math.Min(batchSize, Count - (int)offset);
        GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedInt, offset * _commands.Current.Stride, batchCount, 0);

        // _vao.Unbind();
        // EBO.Unbind();
        _commands.Current.Unbind();

        // _commands.Swap();
    }
    
    public void Dispose()
    {
        _commands.Dispose();
        _instanceData.Dispose();
        _drawData.Dispose();
        
        _vao.Dispose();
        EBO.Dispose();
        VBO.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}, {typeof(TInstanceData).Name}>:");
        builder.AppendLine($"    Commands:     {_commands.Current.GetFormattedSpace()}");
        builder.AppendLine($"    InstanceData: {_instanceData.GetFormattedSpace()}");
        builder.AppendLine($"    DrawData:     {_drawData.GetFormattedSpace()}");
        builder.AppendLine($"    Indices:      {EBO.GetFormattedSpace()}");
        builder.AppendLine($"    Vertices:     {VBO.GetFormattedSpace()}");
        return builder.ToString();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}
