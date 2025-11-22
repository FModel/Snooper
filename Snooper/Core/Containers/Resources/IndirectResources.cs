using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public struct AllocationCounts
{
    public uint Components; // total number of components in the system
    public uint UniqueComponents; // number of unique components (based on descriptor guid)
    public uint Instances; // total number of instances across all components
    public uint Draws; // we have one draw call per section in LOD0 per component
    public uint Materials; // total number of materials across all components
    public uint Sections; // total number of sections across all LODs of all unique components
    public uint Indices; // total number of indices across all LODs of all unique components
    public uint Vertices; // total number of vertices across all LODs of all unique components
    public uint ColoredVertices; // total number of vertices with color data across all LODs of all unique components
}

public class IndirectResources<TVertex, TInstanceData, TPerMaterialData>(PrimitiveType type) : IMemoryDetailsProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    private readonly GeometryPool<TVertex> _geometry = new();
    private readonly DoubleBuffer<DrawIndirectBuffer> _commands = new(() => new DrawIndirectBuffer());
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new();
    private readonly ShaderStorageBuffer<TPerMaterialData> _materialData = new();
    
    private readonly List<Action> _geometryUpdates = []; // TODO: remove this hack
    
    public void Generate()
    {
        _geometry.Generate();
        _commands.Generate();
        _instanceData.Generate();
        _materialData.Generate();
    }
    
    public void SetVertexLayout(Action<uint> setter) => _geometry.SetVertexLayout(setter);
    
    public void Allocate(AllocationCounts counts)
    {
        _geometry.Allocate(counts);
        _commands.Current.Allocate(counts.Draws);
        _instanceData.Allocate(counts.Instances);
        _materialData.Allocate(counts.Materials);
        
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
    
    public ResourcesMetadata Add(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Materials.Length == 0)
            throw new InvalidOperationException("Primitive component must have at least one material assigned before being added to IndirectResources.");
        
        var primitive = component.Descriptor;
        var geometryHandle = _geometry.Add(primitive.Guid, primitive.Lods, primitive.Bounds);
        var instanceAllocation = _instanceData.AddRange(component.GetPerInstanceData());
        foreach (var material in component.Materials)
        {
            material.Allocation = _materialData.Add(new TPerMaterialData());
        }

        var instanceCount = component.IsVisible ? (uint)instanceAllocation.Length : 0;
        var baseMaterial = 0u;
        if (component.Materials[0].Allocation is { } allocation)
        {
            baseMaterial = (uint)allocation.StartIndex;
        }

        const uint currentLod = 0u;
        var drawAllocations = new BufferAllocation[primitive.Lods[currentLod].Sections.Length];
        for (var i = 0u; i < drawAllocations.Length; i++)
        {
            var section = primitive.Lods[currentLod].Sections[i];
            drawAllocations[i] = _commands.Current.Add(new DrawElementsIndirectCommand
            {
                IndexCount = section.IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = geometryHandle.FirstIndex + section.FirstIndex,
                BaseVertex = geometryHandle.BaseVertex,
                BaseInstance = (uint)instanceAllocation.StartIndex,
                BaseGeometry = (uint)geometryHandle.CullingAllocation.StartIndex,
                BaseColor = geometryHandle.BaseColor,
                BaseMaterial = baseMaterial,
                MaterialIndex = section.MaterialIndex,
                PickingId = component.Id,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = (uint)instanceAllocation.StartIndex,
                SectionId = i,
            });
        }
        
        component.MarkClean(DirtyFlags.All);
        return new ResourcesMetadata(geometryHandle, instanceAllocation, component.Materials[0].Allocation!.Value, drawAllocations);
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Metadata is not { } metadata) return;
        
        if (metadata.GeometryHandle.IsDirty)
        {
            _geometryUpdates.Add(() =>
            {
                _geometry.UpdateOverrideLod(metadata.GeometryHandle);
                metadata.GeometryHandle.MarkClean();
            });
        }
        
        if (component.IsDirty(DirtyFlags.InstanceData))
        {
            _instanceData.QueueUpdate(metadata.InstanceAllocation, component.GetPerInstanceData());
            component.MarkClean(DirtyFlags.InstanceData);
        }

        if (component.IsDirty(DirtyFlags.Visibility))
        {
            const int offset = 40; // offset to OriginalInstanceCount in DrawElementsIndirectCommand
            if (component.IsVisible)
            {
                var originalInstanceCount = (uint)metadata.InstanceAllocation.Length;
                foreach (var drawAllocation in metadata.DrawAllocations)
                {
                    _commands.Current.UpdateCustom(drawAllocation, originalInstanceCount, offset);
                    _commands.Current.UpdateCustom(drawAllocation, originalInstanceCount, 4);
                }
            }
            else foreach (var drawAllocation in metadata.DrawAllocations)
            {
                _commands.Current.UpdateCustom(drawAllocation, 0u, offset);
                _commands.Current.UpdateCustom(drawAllocation, 0u, 4);
            }
            component.MarkClean(DirtyFlags.Visibility);
        }
    }
    
    public void Update(BufferAllocation allocation, TPerMaterialData materialData)
    {
        if (!materialData.IsReady) 
            throw new InvalidOperationException("Material data is not ready.");

        _materialData.QueueUpdate(allocation, materialData);
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
    
    public void Remove(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Metadata is not { } metadata) return;
        
        Log.Debug("Removing component {ComponentId}, freeing {DrawsCount} draws, {InstancesCount} instances, {MaterialsCount} materials.",
            component.Id,
            metadata.DrawAllocations.Length,
            metadata.InstanceAllocation.Length,
            metadata.MaterialAllocation.Length);
        
        _geometry.Remove(metadata.GeometryHandle);
        _commands.Current.RemoveRange(metadata.DrawAllocations);
        _instanceData.Remove(metadata.InstanceAllocation);
        _materialData.Remove(metadata.MaterialAllocation);
    }

    public void Cull(CameraComponent camera) => _geometry.Cull(camera, _instanceData, _commands.Current);

    public void Render()
    {
        _commands.Current.Bind();
        _commands.Current.Bind(0);
        _instanceData.Bind(1);
        _materialData.Bind(2);
        
        _geometry.Render(() => GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedInt, 0, _commands.Current.MaxCountHeld, _commands.Current.Stride));

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
}