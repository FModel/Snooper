using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Buffers;

public sealed class IndirectDrawBuffer(BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : IBufferStatisticsProvider, IDisposable
{
    public DrawIndirectBuffer Commands { get; } = new(usageHint);
    public ShaderStorageBuffer<PerDrawData> DrawData { get; } = new(usageHint);

    public int Capacity => Commands.Capacity;
    public int Stride => Commands.Stride;

    public void Generate()
    {
        Commands.Generate();
        DrawData.Generate();
    }

    public void Allocate(uint size)
    {
        Commands.Allocate(size);
        DrawData.Allocate(size);
    }

    public DrawAllocation Add(DrawElementsIndirectCommand command, PerDrawData data)
    {
        var commandAllocation = Commands.Add(command);
        var dataAllocation = DrawData.Add(data);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex, "Draw command and per-draw data buffers must stay index-aligned.");
        return new DrawAllocation(commandAllocation, dataAllocation);
    }

    public DrawAllocation CopyFrom(IndirectDrawBuffer source, DrawAllocation allocation)
    {
        var commandAllocation = Commands.CopyFrom(source.Commands, allocation.Command);
        var dataAllocation = DrawData.CopyFrom(source.DrawData, allocation.Data);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex, "Draw command and per-draw data buffers must stay index-aligned.");
        return new DrawAllocation(commandAllocation, dataAllocation);
    }

    public void Remove(DrawAllocation allocation)
    {
        Commands.Remove(allocation.Command);
        DrawData.Remove(allocation.Data);
    }

    public void Clear()
    {
        Commands.Clear();
        DrawData.Clear();
    }

    public readonly struct DeferMergeScope(IndirectDrawBuffer buffer) : IDisposable
    {
        private readonly Buffer<DrawElementsIndirectCommand>.DeferMergeScope _commandScope = buffer.Commands.DeferMerge();
        private readonly Buffer<PerDrawData>.DeferMergeScope _dataScope = buffer.DrawData.DeferMerge();

        public void Dispose()
        {
            _commandScope.Dispose();
            _dataScope.Dispose();
        }
    }

    public DeferMergeScope DeferMerge() => new(this);

    public void Dispose()
    {
        Commands.Dispose();
        DrawData.Dispose();
    }

    public long Allocated => Commands.Allocated + DrawData.Allocated;
    public long Used => Commands.Used + DrawData.Used;
    public BufferStatistics? GetBufferStatistics() => Commands.GetBufferStatistics();
}

public readonly struct DrawAllocation(BufferAllocation command, BufferAllocation data)
{
    public readonly BufferAllocation Command = command;
    public readonly BufferAllocation Data = data;
}

public readonly struct PerDrawData(GeometryHandle geometry, uint sectionId, uint baseMaterial, LodSectionDescriptor section, uint pickingId, DrawElementsIndirectCommand command, bool castShadow)
{
    public readonly uint MeshIndex = geometry.MeshIndex; // index into the per-mesh buffers (PerMeshData, PrimitiveOffsets)
    public readonly uint SectionId = sectionId; // section index in the current model (0-X)
    public readonly uint BaseMaterial = baseMaterial; // offset of the first material this component uses in the material buffer
    public readonly uint MaterialIndex = section.MaterialIndex; // index of the material relative to BaseMaterial (GPU-written per LOD)
    public readonly uint PickingId = pickingId;
    public readonly uint OriginalInstanceCount = command.InstanceCount;
    public readonly uint OriginalBaseInstance = command.BaseInstance;
    public readonly uint CastShadow = section.CastShadow && castShadow ? 1u : 0u; // 0 or 1
    public readonly uint Lod; // GPU-written, LOD chosen by the culling pass, so it takes no parameter
    public readonly uint BaseColor = geometry.BaseColor; // GPU-written per LOD, offset into the vertex color buffer

    public static readonly int OriginalInstanceCountOffset = (int)Marshal.OffsetOf<PerDrawData>(nameof(OriginalInstanceCount));
}
