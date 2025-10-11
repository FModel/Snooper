using System.Text;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex, TInstanceData, TPerDrawData>(int initialDrawCapacity, PrimitiveType type)
    : IMemorySizeProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerDrawData : unmanaged, IPerDrawData
{
    private readonly GeometryPool<TVertex> _geometry = new(initialDrawCapacity);
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TPerDrawData> _drawData = new(initialDrawCapacity);
    
    public void Generate()
    {
        _geometry.Generate();
        _commands.Generate();
        _instanceData.Generate();
        _drawData.Generate();
    }
    
    public void SetVertexLayout(Action<int> setter) => _geometry.SetVertexLayout(setter);
    
    public void Allocate(uint componentCount, uint drawCount, uint indices, uint vertices)
    {
        _geometry.Allocate(componentCount, drawCount, indices, vertices);

        _commands.Current.Bind();
        _commands.Current.Allocate(new DrawElementsIndirectCommand[drawCount]);
        _commands.Current.Unbind();

        _instanceData.Bind();
        _instanceData.Allocate(new TInstanceData[drawCount * 2]);
        _instanceData.Unbind();

        _drawData.Bind();
        _drawData.Allocate(new TPerDrawData[drawCount]);
        _drawData.Unbind();
    }
    
    public void Add(uint pickingId, PrimitiveDescriptor<TVertex> primitive, MaterialSection[] materials, TInstanceData[] instanceData)
    {
        var handle = _geometry.Add(primitive.Guid, primitive.Lods, primitive.Bounds);
        
        _instanceData.Bind();
        var baseInstance = (uint)_instanceData.AddRange(instanceData);
        _instanceData.Unbind();
        
        _commands.Current.Bind();
        var instanceCount = (uint)instanceData.Length;
        for (var i = 0u; i < materials.Length; i++)
        {
            materials[i].DrawMetadata.BaseInstance = (int)baseInstance;
            materials[i].DrawMetadata.ModelId = handle.ModelId;
            materials[i].DrawMetadata.DrawId = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = primitive.Lods[0].Sections[i].IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = handle.FirstIndex + primitive.Lods[0].Sections[i].FirstIndex,
                BaseVertex = handle.BaseVertex,
                BaseInstance = baseInstance,
                PickingId = pickingId,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = baseInstance,
                ModelId = handle.ModelId,
                SectionId = i,
            });
        }
        _commands.Current.Unbind();
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerDrawData> component)
    {
        if (component.Materials.Length < 1) return;
        
        var metadata = component.Materials[0].DrawMetadata;
        
        if (metadata.OverrideLod != component.OverrideLod)
        {
            _geometry.UpdateOverrideLod(metadata.ModelId, component.OverrideLod);
            component.Materials[0].DrawMetadata.OverrideLod = component.OverrideLod;
        }
        
        if (component.IsDirty)
        {
            _instanceData.Bind();
            _instanceData.Update(metadata.BaseInstance, component.GetPerInstanceData());
            _instanceData.Unbind();
            
            component.MarkClean();
        }
    }
    
    public void Update(int drawId, TPerDrawData drawData)
    {
        if (!drawData.IsReady) throw new InvalidOperationException("Draw data is not ready.");
        Log.Debug("Updating draw data for draw ID {DrawId}.", drawId);

        _drawData.Bind();
        _drawData.Update(drawId, drawData);
        _drawData.Unbind();
    }

    public void Remove(IndirectDrawMetadata metadata)
    {
        Log.Debug("Removing draw data for draw ID {DrawId}.", metadata.DrawId);
        
        _commands.Current.Bind();
        _commands.Current.Remove(metadata.DrawId);
        _commands.Current.Unbind();
        
        _instanceData.Bind();
        _instanceData.Remove(metadata.BaseInstance);
        _instanceData.Unbind();
        // EBO.Remove();
        // VBO.Remove();

        _drawData.Bind();
        _drawData.Remove(metadata.DrawId);
        _drawData.Unbind();
        
        // _culling.Remove();
    }

    public void Cull(CameraComponent camera) => _geometry.Cull(camera, _instanceData, _commands.Current);

    public void Render()
    {
        _commands.Current.Bind();
        _commands.Current.Bind(0);
        _instanceData.Bind(1);
        _drawData.Bind(2);
        
        _geometry.Render(() => GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedInt, 0, _commands.Current.Count, _commands.Current.Stride));

        _commands.Current.Unbind();
        // _commands.Swap();
    }
    
    public void Dispose()
    {
        _geometry.Dispose();
        _commands.Dispose();
        _instanceData.Dispose();
        _drawData.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}, {typeof(TInstanceData).Name}>:");
        builder.AppendLine(_geometry.GetFormattedSpace());
        builder.AppendLine($"    x{_commands.Current.Count} Commands:     {_commands.Current.GetFormattedSpace()}");
        builder.AppendLine($"    x{_drawData.Count} DrawData:     {_drawData.GetFormattedSpace()}");
        builder.AppendLine($"    x{_instanceData.Count} InstanceData: {_instanceData.GetFormattedSpace()}");
        return builder.ToString();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}