using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public struct AllocationCounts
{
    public uint Components;
    public uint UniqueComponents;
    public uint Instances;
    public uint Draws;
    public uint Materials;
    public uint Indices;
    public uint Vertices;
    public uint ColoredVertices;
}

public class IndirectResources<TVertex, TInstanceData, TPerMaterialData>(int initialDrawCapacity, PrimitiveType type) : IMemoryDetailsProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData 
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    private readonly GeometryPool<TVertex> _geometry = new(initialDrawCapacity);
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer(initialDrawCapacity));
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<TPerMaterialData> _materialData = new(initialDrawCapacity);
    
    private readonly List<Action> _geometryUpdates = []; // TODO: remove this hack
    
    public void Generate()
    {
        _geometry.Generate();
        _commands.Generate();
        _instanceData.Generate();
        _materialData.Generate();
    }
    
    public void SetVertexLayout(Action<int> setter) => _geometry.SetVertexLayout(setter);
    
    public void Allocate(AllocationCounts counts)
    {
        _geometry.Allocate(counts);

        _commands.Current.Bind();
        _commands.Current.Allocate(new DrawElementsIndirectCommand[counts.Draws]);
        _commands.Current.Unbind();

        _instanceData.Bind();
        _instanceData.Allocate(new TInstanceData[counts.Instances]);
        _instanceData.Unbind();

        _materialData.Bind();
        _materialData.Allocate(new TPerMaterialData[counts.Materials]);
        _materialData.Unbind();
        
        Log.Debug("Allocated IndirectResources<{VertexTypeName}, {InstanceTypeName}, {PerMaterialTypeName}> for {ComponentsCount} components ({UniqueComponents} unique ones): {DrawsCount} draws, {InstancesCount} instances, {MaterialsCount} materials, {IndicesCount} indices, {VerticesCount} vertices, {ColoredVerticesCount} colored vertices.",
            typeof(TVertex).Name,
            typeof(TInstanceData).Name,
            typeof(TPerMaterialData).Name,
            counts.Components,
            counts.UniqueComponents,
            counts.Draws,
            counts.Instances,
            counts.Materials,
            counts.Indices,
            counts.Vertices,
            counts.ColoredVertices);
    }
    
    public ResourcesMetadata Add(uint pickingId, PrimitiveDescriptor<TVertex> primitive, MaterialSection[] materials, TInstanceData[] instanceData)
    {
        var handle = _geometry.Add(primitive.Guid, primitive.Lods, primitive.Bounds);
        
        _instanceData.Bind();
        var baseInstance = (uint)_instanceData.AddRange(instanceData);
        _instanceData.Unbind();
        
        _materialData.Bind();
        var baseMaterial = (uint)_materialData.AddRange(new TPerMaterialData[materials.Length]);
        for (var i = 0u; i < materials.Length; i++)
        {
            materials[i].MaterialOffset = baseMaterial + i;
        }
        _materialData.Unbind();

        _commands.Current.Bind();
        var instanceCount = (uint)instanceData.Length;
        const uint currentLod = 0u;
        var drawIds = new int[primitive.Lods[currentLod].Sections.Length];
        for (var i = 0u; i < drawIds.Length; i++)
        {
            drawIds[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = primitive.Lods[currentLod].Sections[i].IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = handle.FirstIndex + primitive.Lods[currentLod].Sections[i].FirstIndex,
                BaseVertex = handle.BaseVertex,
                BaseInstance = baseInstance,
                BaseGeometry = handle.BaseGeometry,
                BaseColor = handle.BaseColor,
                BaseMaterial = baseMaterial,
                MaterialIndex = primitive.Lods[currentLod].Sections[i].MaterialIndex,
                PickingId = pickingId,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = baseInstance,
                SectionId = i,
            });
        }
        _commands.Current.Unbind();
        
        return new ResourcesMetadata(handle, (int)baseInstance, (int)baseMaterial, drawIds);
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Metadata is not { } metadata) return;
        
        if (metadata.GeometryHandle.IsDirty)
        {
            _geometryUpdates.Add(() =>
            {
                _geometry.UpdateOverrideLod((int)metadata.GeometryHandle.BaseGeometry, metadata.GeometryHandle.OverrideLod);
                metadata.GeometryHandle.MarkClean();
            });
        }
        
        if (component.IsDirty)
        {
            _instanceData.QueueUpdate(metadata.BaseInstance, component.GetPerInstanceData());
            component.MarkClean();
        }
    }
    
    public void Update(int offset, TPerMaterialData materialData)
    {
        if (!materialData.IsReady) 
            throw new InvalidOperationException("Material data is not ready.");

        _materialData.QueueUpdate(offset, materialData);
    }
    
    public void FlushUpdates()
    {
        if (_geometryUpdates.Count > 0)
        {
            foreach (var update in _geometryUpdates)
                update();
            _geometryUpdates.Clear();
        }
        
        _instanceData.FlushUpdates();
        _materialData.FlushUpdates();
    }
    
    public void Remove(ResourcesMetadata metadata)
    {
        Log.Debug("Removing primitive with Geometry {GeometryId} and {SectionCount} sections.", metadata.GeometryHandle.BaseGeometry, metadata.DrawIds.Length);
        
        // TODO: properly do this
        
        // Remove all draw commands for each section
        _commands.Current.Bind();
        foreach (var drawId in metadata.DrawIds)
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
        _materialData.Remove(metadata.BaseMaterial);
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

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _geometry.Allocated;
            total += _commands.Allocated;
            total += _instanceData.Allocated;
            total += _materialData.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _geometry.Used;
            total += _commands.Used;
            total += _instanceData.Used;
            total += _materialData.Used;
            return total;
        }
    }
    
    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Geometry Pool", _geometry);
        yield return new MemoryDetail("Draw Commands", _commands.Current);
        yield return new MemoryDetail("Instance Data", _instanceData);
        yield return new MemoryDetail("Material Data", _materialData);
    }

    public GetPName Name => throw new NotImplementedException();
    public int PreviousHandle => throw new NotImplementedException();
}