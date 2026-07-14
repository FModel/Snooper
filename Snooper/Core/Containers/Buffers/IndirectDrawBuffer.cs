using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

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

public readonly struct PerDrawData
{
    public uint MeshIndex { get; init; } // index into the per-mesh buffers (PerMeshData, PrimitiveOffsets)
    public uint SectionId { get; init; } // section index in the current model (0-X)
    public uint BaseMaterial { get; init; } // offset of the first material this component uses in the material buffer
    public uint MaterialIndex { get; init; } // index of the material relative to BaseMaterial (GPU-written per LOD)
    public uint PickingId { get; init; }
    public uint OriginalInstanceCount { get; init; }
    public uint OriginalBaseInstance { get; init; }
    public uint CastShadow { get; init; } // 0 or 1
    public readonly uint Lod; // GPU-written, LOD chosen by the culling pass
    public uint BaseColor { get; init; } // GPU-written per LOD, offset into the vertex color buffer

    public const int OriginalInstanceCountOffset = 20; // byte offset for partial updates
}
