using System.Text;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public class IndirectResources<TVertex, TInstanceData, TPerMaterialData>(int initialDrawCapacity, PrimitiveType type)
    : IMemorySizeProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    private readonly GeometryPool<TVertex> _geometry = new(initialDrawCapacity);
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TPerMaterialData> _materialData = new(initialDrawCapacity);
    
    public void Generate()
    {
        _geometry.Generate();
        _commands.Generate();
        _instanceData.Generate();
        _materialData.Generate();
    }
    
    public void SetVertexLayout(Action<int> setter) => _geometry.SetVertexLayout(setter);
    
    public void Allocate(uint componentCount, uint drawCount, uint materialCount, uint indices, uint vertices)
    {
        _geometry.Allocate(componentCount, drawCount, indices, vertices);

        _commands.Current.Bind();
        _commands.Current.Allocate(new DrawElementsIndirectCommand[drawCount]);
        _commands.Current.Unbind();

        _instanceData.Bind();
        _instanceData.Allocate(new TInstanceData[drawCount * 2]);
        _instanceData.Unbind();

        _materialData.Bind();
        _materialData.Allocate(new TPerMaterialData[materialCount]);
        _materialData.Unbind();
    }
    
    public ResourcesMetadata Add(uint pickingId, PrimitiveDescriptor<TVertex> primitive, MaterialSection[] materials, TInstanceData[] instanceData)
    {
        var handle = _geometry.Add(primitive.Guid, primitive.Lods, primitive.Bounds);
        
        _instanceData.Bind();
        var baseInstance = (uint)_instanceData.AddRange(instanceData);
        _instanceData.Unbind();
        
        _materialData.Bind();
        var baseMaterialOffset = (uint)_materialData.AddRange(new TPerMaterialData[materials.Length]);
        for (var i = 0u; i < materials.Length; i++)
        {
            materials[i].MaterialOffset = baseMaterialOffset + i;
        }
        _materialData.Unbind();

        _commands.Current.Bind();
        var instanceCount = (uint)instanceData.Length;
        var sectionDrawIds = new int[primitive.Lods[0].Sections.Length];
        for (var i = 0u; i < sectionDrawIds.Length; i++)
        {
            sectionDrawIds[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = primitive.Lods[0].Sections[i].IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = handle.FirstIndex + primitive.Lods[0].Sections[i].FirstIndex,
                BaseVertex = handle.BaseVertex,
                BaseInstance = baseInstance,
                BaseMaterialOffset = baseMaterialOffset,
                MaterialIndex = primitive.Lods[0].Sections[i].MaterialIndex,
                PickingId = pickingId,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = baseInstance,
                ModelId = handle.ModelId,
                SectionId = i,
            });
        }
        _commands.Current.Unbind();
        
        return new ResourcesMetadata
        {
            ModelId = handle.ModelId,
            BaseInstance = (int)baseInstance,
            OverrideLod = -1,
            BaseMaterialOffset = baseMaterialOffset,
            SectionDrawIds = sectionDrawIds
        };
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        var metadata = component.Metadata;
        if (!metadata.IsGenerated) return;
        
        if (metadata.OverrideLod != component.OverrideLod)
        {
            _geometry.UpdateOverrideLod(metadata.ModelId, component.OverrideLod);
            metadata.OverrideLod = component.OverrideLod;
        }
        
        if (component.IsDirty)
        {
            _instanceData.Bind();
            _instanceData.Update(metadata.BaseInstance, component.GetPerInstanceData());
            _instanceData.Unbind();
            
            component.MarkClean();
        }
    }
    
    public void Update(int offset, TPerMaterialData materialData)
    {
        if (!materialData.IsReady) throw new InvalidOperationException("Material data is not ready.");
        Log.Debug("Updating material data at offset {Offset}.", offset);

        _materialData.Bind();
        _materialData.Update(offset, materialData);
        _materialData.Unbind();
    }

    public void Remove(ResourcesMetadata metadata)
    {
        Log.Debug("Removing primitive with ModelId {ModelId} and {SectionCount} sections.", metadata.ModelId, metadata.SectionDrawIds.Length);
        
        // TODO: properly do this
        
        // Remove all draw commands for each section
        _commands.Current.Bind();
        foreach (var drawId in metadata.SectionDrawIds)
        {
            _commands.Current.Remove(drawId);
        }
        _commands.Current.Unbind();
        
        // Remove instance data
        _instanceData.Bind();
        _instanceData.Remove(metadata.BaseInstance);
        _instanceData.Unbind();

        // Remove material data for all materials
        _materialData.Bind();
        _materialData.Remove((int)metadata.BaseMaterialOffset);
        _materialData.Unbind();
    }

    public void Cull(CameraComponent camera) => _geometry.Cull(camera, _instanceData, _commands.Current);

    public void Render()
    {
        _commands.Current.Bind();
        _commands.Current.Bind(0);
        _instanceData.Bind(1);
        _materialData.Bind(2);
        
        _geometry.Render(() => GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedInt, 0, _commands.Current.Count, _commands.Current.Stride));

        _commands.Current.Unbind();
        // _commands.Swap();
    }
    
    public void Dispose()
    {
        _geometry.Dispose();
        _commands.Dispose();
        _instanceData.Dispose();
        _materialData.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IndirectResources<{typeof(TVertex).Name}, {typeof(TInstanceData).Name}>:");
        builder.AppendLine(_geometry.GetFormattedSpace());
        builder.AppendLine($"    x{_commands.Current.Count} Commands: {_commands.Current.GetFormattedSpace()}");
        builder.AppendLine($"    x{_materialData.Count} MaterialData: {_materialData.GetFormattedSpace()}");
        builder.AppendLine($"    x{_instanceData.Count} InstanceData: {_instanceData.GetFormattedSpace()}");
        return builder.ToString();
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}