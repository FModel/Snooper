using System.Text;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Mesh;
using Snooper.Rendering.Primitives;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex, TInstanceData, TDrawData>(int initialDrawCapacity, PrimitiveType type)
    : IBind, IMemorySizeProvider
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TDrawData : unmanaged, IPerDrawData
{
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TDrawData> _drawData = new(initialDrawCapacity);

    private readonly VertexArray _vao = new();
    public readonly ElementArrayBuffer<uint> EBO = new(initialDrawCapacity * 200);
    public readonly ArrayBuffer<TVertex> VBO = new(initialDrawCapacity * 100);
    
    public int Count => _commands.Current.Count;

    public void Generate()
    {
        _commands.Generate();
        _instanceData.Generate();
        _drawData.Generate();
        
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

    public void Unbind()
    {
        _commands.Current.Unbind();
        _instanceData.Unbind();
        
        _vao.Unbind();
        EBO.Unbind();
        VBO.Unbind();
    }
    
    public IndirectDrawMetadata Add(TPrimitiveData<TVertex> primitive, MeshMaterialSection[] materialSections, TInstanceData[] instanceData)
    {
        var firstIndex = EBO.AddRange(primitive.Indices);
        var baseVertex = VBO.AddRange(primitive.Vertices);
        var baseInstance = _instanceData.AddRange(instanceData);

        var metadata = new IndirectDrawMetadata { DrawIds = new int[materialSections.Length], BaseInstance = baseInstance };
        for (var i = 0; i < materialSections.Length; i++)
        {
            var materialSection = materialSections[i];
            metadata.DrawIds[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = (uint)materialSection.IndexCount,
                InstanceCount = (uint)instanceData.Length,
                FirstIndex = (uint)(firstIndex + materialSection.FirstIndex),
                BaseVertex = (uint)baseVertex,
                BaseInstance = (uint)baseInstance
            });
        }

        return metadata;
    }

    public void Add(IndirectDrawMetadata metadata, TDrawData[] drawData)
    {
        _drawData.Bind();
        for (var i = 0; i < drawData.Length; i++)
        {
            _drawData.Insert(metadata.DrawIds[i], drawData[i]);
        }
        _drawData.Unbind();
    }

    public void Update(TPrimitiveComponent<TVertex, TInstanceData, TDrawData> component)
    {
        if (!component.Actor.IsDirty) return;
        
        var instanceCount = component.Actor.VisibleInstances.End.Value - component.Actor.VisibleInstances.Start.Value;
        var baseInstance = component.DrawMetadata.BaseInstance + component.Actor.VisibleInstances.Start.Value;
        _commands.Current.UpdateInstance(component.DrawMetadata.DrawIds, component.Actor.IsVisible ? (uint)instanceCount : 0u, (uint)baseInstance);
        
        _instanceData.Update(component.DrawMetadata.BaseInstance, component.GetPerInstanceData());
        component.Actor.MarkClean();
    }

    public void UpdateVertices(int drawId, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        VBO.Update((int) command.BaseVertex, vertices);
    }

    public void UpdatePrimitive(int drawId, uint[] indices, TVertex[] vertices)
    {
        var command = _commands.Current[drawId];
        EBO.Update((int) command.FirstIndex, indices);
        VBO.Update((int) command.BaseVertex, vertices);

        _commands.Current.UpdateCount(drawId, (uint) indices.Length);
    }

    public void Remove(IndirectDrawMetadata metadata)
    {
        _commands.Current.Bind();
        _commands.Current.RemoveRange(metadata.DrawIds);
        _commands.Current.Unbind();

        _instanceData.Bind();
        _instanceData.Remove(metadata.BaseInstance);
        _instanceData.Unbind();

        _drawData.Bind();
        _drawData.RemoveRange(metadata.DrawIds);
        _drawData.Unbind();
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

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}>:");
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
