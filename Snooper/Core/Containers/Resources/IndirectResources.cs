using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;
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
    public uint SkinnedVertices; // total number of vertices with bone data across all LODs of all unique components
    public uint Bones; // total number of bones across all unique components
}

public class IndirectResources<TVertex, TInstanceData, TPerMaterialData>(PrimitiveType mode) : IMemoryDetailsProvider, IDisposable
    where TVertex : unmanaged
    where TInstanceData : unmanaged, IPerInstanceData
    where TPerMaterialData : unmanaged, IPerMaterialData
{
    private readonly GeometryPool<TVertex> _geometry = new();
    private readonly CommandBufferSet _commands = new();
    private readonly ShaderStorageBuffer<TInstanceData> _instanceData = new();
    private readonly ShaderStorageBuffer<TPerMaterialData> _materialData = new();
    private readonly ShaderStorageBuffer<Matrix4x4> _poseData = new(BufferUsageHint.DynamicDraw);

    private readonly List<Action> _geometryUpdates = []; // TODO: remove this hack

    public void Generate()
    {
        _geometry.Generate();
        _commands.Generate();
        _instanceData.Generate();
        _materialData.Generate();
        _poseData.Generate();
    }

    public void SetVertexLayout(Action<uint> setter) => _geometry.SetVertexLayout(setter);

    public void Allocate(AllocationCounts counts, string systemName)
    {
        _geometry.Allocate(counts);
        if (counts.Draws > 0) _commands.Allocate(counts.Draws);
        if (counts.Instances > 0) _instanceData.Allocate(counts.Instances);
        if (counts.Materials > 0) _materialData.Allocate(counts.Materials);
        if (counts.Bones > 0) _poseData.Allocate(counts.Bones);

        Log.Debug("Allocated {SystemName}<{VertexTypeName}, {InstanceTypeName}, {PerMaterialTypeName}> for {ComponentsCount} components ({UniqueComponents} unique ones): {DrawsCount} draws, {InstancesCount} instances, {MaterialsCount} materials, {IndicesCount} indices, {VerticesCount} vertices, {ColoredVerticesCount} colored vertices.",
            systemName,
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
        var descriptor = component.Descriptor;
        var geometryHandle = _geometry.Add(descriptor.Guid, descriptor.Lods, descriptor.Bounds, descriptor.Skeleton);
        var instanceAllocation = _instanceData.AddRange(component.GetPerInstanceData());

        if (descriptor.Skeleton is { } skeleton)
        {
            skeleton._poseAllocation = _poseData.AddRange(skeleton.BoneMatrices);
        }

        BufferAllocation? materialAllocation = null;
        foreach (var material in component.Materials)
        {
            material.Allocation = _materialData.Add(new TPerMaterialData());
            materialAllocation ??= material.Allocation;
        }

        const uint currentLod = 0u;
        var drawAllocations = new DrawBufferAllocation[descriptor.Lods[currentLod].Sections.Length];

        var instanceCount = component.IsVisible ? (uint)instanceAllocation.Length : 0;
        var bufferType = component.IsOpaque ? CommandBufferType.Opaque : CommandBufferType.Transparent;
        var buffer = _commands.GetBuffer(bufferType);
        for (var i = 0u; i < drawAllocations.Length; i++)
        {
            var section = descriptor.Lods[currentLod].Sections[i];
            var draw = buffer.Add(new DrawElementsIndirectCommand
            {
                IndexCount = section.IndexCount,
                InstanceCount = instanceCount,
                FirstIndex = geometryHandle.FirstIndex + section.FirstIndex,
                BaseVertex = geometryHandle.BaseVertex,
                BaseInstance = (uint)instanceAllocation.StartIndex,
                BaseGeometry = (uint)geometryHandle.CullingAllocation.StartIndex,
                BaseColor = geometryHandle.BaseColor,
                BaseBoneInfluence = geometryHandle.BaseBoneInfluence,
                BaseBone = (uint)(geometryHandle.BoneAllocation?.StartIndex ?? int.MaxValue),
                BasePose = (uint)(descriptor.Skeleton?._poseAllocation?.StartIndex ?? int.MaxValue),
                BaseMaterial = (uint)(materialAllocation?.StartIndex ?? int.MaxValue),
                MaterialIndex = section.MaterialIndex,
                PickingId = (uint)component.Id,
                OriginalInstanceCount = instanceCount,
                OriginalBaseInstance = (uint)instanceAllocation.StartIndex,
                SectionId = i,
                CastShadow = section.CastShadow && component.CastShadow ? 1u : 0u
            });

            drawAllocations[i] = new DrawBufferAllocation(draw, bufferType, section.MaterialIndex);
        }

        component.MarkClean(DirtyFlags.All);
        return new ResourcesMetadata(geometryHandle, instanceAllocation, materialAllocation, drawAllocations);
    }

    public void Update(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Metadata is not { } metadata) return;

        if (component.IsDirty(DirtyFlags.Opacity))
        {
            foreach (var draw in metadata.DrawAllocations)
            {
                if (!component.Materials[draw.MaterialIndex].IsTranslucent) continue;
                draw.BufferAllocation = _commands.Transfer(draw.BufferAllocation, draw.BufferType, CommandBufferType.Transparent);
                draw.BufferType = CommandBufferType.Transparent;
            }

            component.MarkClean(DirtyFlags.Opacity);
        }

        if (component.IsDirty(DirtyFlags.Outline))
        {
            if (component.IsOutlined)
                foreach (var draw in metadata.DrawAllocations)
                    _commands.Transfer(draw.BufferAllocation, draw.BufferType, CommandBufferType.Mask);

            component.MarkClean(DirtyFlags.Outline);
        }

        if (component.IsDirty(DirtyFlags.ManualLodSwap))
        {
            _geometryUpdates.Add(() => _geometry.UpdateOverrideLod(metadata.GeometryHandle));
            component.MarkClean(DirtyFlags.ManualLodSwap);
        }

        if (component.IsDirty(DirtyFlags.InstanceData))
        {
            _instanceData.QueueUpdate(metadata.InstanceAllocation, component.GetPerInstanceData());
            component.MarkClean(DirtyFlags.InstanceData);
        }

        if (component.IsDirty(DirtyFlags.Animation))
        {
            if (component.Descriptor.Skeleton is { _poseAllocation: { } poseAllocation } skeleton)
                _poseData.Update(poseAllocation, skeleton.BoneMatrices);

            component.MarkClean(DirtyFlags.Animation);
            foreach (var child in component.Children)
            {
                child.MarkDirty(DirtyFlags.Transform);
            }
        }

        if (component.IsDirty(DirtyFlags.Visibility))
        {
            const int offset = 52; // offset to OriginalInstanceCount in DrawElementsIndirectCommand

            var originalInstanceCount = component.IsVisible ? (uint)metadata.InstanceAllocation.Length : 0u;
            foreach (var draw in metadata.DrawAllocations)
            {
                var buffer = _commands.GetBuffer(draw.BufferType);
                buffer.UpdateCustom(draw.BufferAllocation, originalInstanceCount, offset);
                buffer.UpdateCustom(draw.BufferAllocation, originalInstanceCount, 4);
            }

            component.MarkClean(DirtyFlags.Visibility);
        }
    }

    public void Update(MaterialSection material)
    {
        if (material.Allocation is not { } allocation)
            throw new InvalidOperationException("Material section allocation is null.");
        if (material.MaterialDataContainer is not { } container)
            throw new InvalidOperationException("Material data container is null.");
        if (container.Raw is not TPerMaterialData raw)
            throw new InvalidOperationException($"Material data container raw type {container.Raw?.GetType()} does not match expected type {typeof(TPerMaterialData)}.");

        // TODO: remove duplicates in GPU memory
        _materialData.QueueUpdate(allocation, raw);
    }

    public void ClearMaskBuffer() => _commands.ClearMask();
    public void BeginDeferMerge() => _commands.BeginDeferMerge();
    public void EndDeferMerge() => _commands.EndDeferMerge();

    public void Flush()
    {
        if (_geometryUpdates.Count > 0)
        {
            foreach (var update in _geometryUpdates)
                update();
            _geometryUpdates.Clear();
        }

        _instanceData.FlushUpdates();
        _materialData.FlushUpdates();
        _poseData.FlushUpdates();
    }

    public void Remove(PrimitiveComponent<TVertex, TInstanceData, TPerMaterialData> component)
    {
        if (component.Metadata is not { } metadata) return;

        Log.Debug("Removing component {ComponentId}, freeing {DrawsCount} draws, {InstancesCount} instances, {MaterialsCount} materials.",
            component.Id,
            metadata.DrawAllocations.Length,
            metadata.InstanceAllocation.Length,
            metadata.MaterialAllocation?.Length);

        _geometry.Remove(metadata.GeometryHandle);
        foreach (var draw in metadata.DrawAllocations)
            _commands.GetBuffer(draw.BufferType).Remove(draw.BufferAllocation);
        _instanceData.Remove(metadata.InstanceAllocation);
        if (metadata.MaterialAllocation is { } materialAllocation)
            _materialData.Remove(materialAllocation);
    }

    public void Cull(IViewProjectionProvider camera, CommandBufferType type, bool shadowPass = false) => _geometry.Cull(camera, _instanceData, _commands.GetBuffer(type), shadowPass);

    public void Render(CommandBufferType type)
    {
        var buffer = _commands.GetBuffer(type);
        buffer.Bind();
        buffer.Bind(0);
        _instanceData.Bind(1);
        _materialData.Bind(2);
        _poseData.Bind(3);

        _geometry.Render(() => GL.MultiDrawElementsIndirect(mode, DrawElementsType.UnsignedInt, 0, buffer.Capacity, buffer.Stride));

        buffer.Unbind();
    }

    /// <summary>
    /// Sort transparent commands from farthest to nearest based on camera position.
    /// TODO: Implement actual sorting logic (compute shader or CPU-side)
    /// </summary>
    public void SortTransparentCommands(IViewProjectionProvider camera)
    {

    }

    public void Dispose()
    {
        _geometry.Dispose();
        _commands.Dispose();
        _instanceData.Dispose();
        _materialData.Dispose();
        _poseData.Dispose();
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
            total += _poseData.Allocated;
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
            total += _poseData.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Geometry Pool", _geometry);
        yield return new MemoryDetail("Draw Commands", _commands);
        yield return new MemoryDetail("Instance Data", _instanceData);
        yield return new MemoryDetail("Material Data", _materialData);
        yield return new MemoryDetail("Pose Data", _poseData);
    }
}
