using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Resources;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Buffers;

public sealed class IndirectDrawBuffer(int viewCount = 1, BufferUsageHint usageHint = BufferUsageHint.StaticDraw) : IBufferStatisticsProvider, IDisposable
{
    public DrawIndirectBuffer Commands { get; } = new(usageHint, viewCount);
    public ShaderStorageBuffer<PerDrawCulled> CulledData { get; } = new(usageHint, viewCount);
    public ShaderStorageBuffer<PerDrawStatic> StaticData { get; } = new(usageHint);
    public ShaderStorageBuffer<uint> ChunkedDraws { get; } = new(usageHint); // indices of draws that are too large to be culled in a single thread
    public const uint ChunkedDrawOffset = 1; // draw 0 is an actual gl_DrawID, and a freed slot is also 0, so we offset indices

    public readonly int ViewCount = viewCount >= 1 ? viewCount : throw new ArgumentOutOfRangeException(nameof(viewCount), viewCount, "A draw buffer needs at least one slice.");
    public int Capacity => Commands.Capacity;
    public int Extent => Commands.Extent;
    public int Stride => Commands.Stride;

    public void Generate()
    {
        Commands.Generate();
        CulledData.Generate();
        StaticData.Generate();
        ChunkedDraws.Generate();
    }

    public void Allocate(uint size, uint chunked)
    {
        Commands.Allocate(size);
        CulledData.Allocate(size);
        StaticData.Allocate(size);
        ChunkedDraws.Allocate(Math.Max(chunked, 1u));
    }

    public DrawAllocation Add(DrawElementsIndirectCommand command, PerDrawStatic data, PerDrawCulled output, uint instanceCount)
    {
        var commandAllocation = Commands.Add(command);
        var dataAllocation = StaticData.Add(data);
        var outputAllocation = CulledData.Add(output);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex && commandAllocation.StartIndex == outputAllocation.StartIndex,
            "Draw command, per-draw data and per-draw output buffers must stay index-aligned.");

        BufferAllocation? chunkedAllocation = null;
        if (instanceCount > Settings.MaxInstancesPerCullThread)
        {
            // this draw is too large to be culled in a single thread
            chunkedAllocation = ChunkedDraws.Add((uint) commandAllocation.StartIndex + ChunkedDrawOffset);
        }

        return new DrawAllocation(commandAllocation, dataAllocation, outputAllocation, chunkedAllocation);
    }

    public DrawAllocation CopyFrom(IndirectDrawBuffer source, DrawAllocation allocation)
    {
        var commandAllocation = Commands.CopyFrom(source.Commands, allocation.Command);
        var dataAllocation = StaticData.CopyFrom(source.StaticData, allocation.Static);
        var outputAllocation = CulledData.CopyFrom(source.CulledData, allocation.Culled);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex && commandAllocation.StartIndex == outputAllocation.StartIndex,
            "Draw command, per-draw data and per-draw output buffers must stay index-aligned.");

        BufferAllocation? chunkedAllocation = null;
        if (allocation.Chunked != null)
        {
            // this draw was chunked in the source buffer, so we need to chunk it in this buffer as well
            chunkedAllocation = ChunkedDraws.Add((uint) commandAllocation.StartIndex + ChunkedDrawOffset);
        }

        return new DrawAllocation(commandAllocation, dataAllocation, outputAllocation, chunkedAllocation);
    }

    public int GetViewBase(int view) => (view < MaskViewIndex ? view : 0) * Capacity;
    public nint GetViewOffset(int view) => GetViewBase(view) * Stride;

    public int MaskViewIndex => ViewCount - 1;
    public int MaskViewBase => MaskViewIndex * Capacity;
    public nint MaskViewOffset => MaskViewBase * Stride;

    public void Remove(DrawAllocation allocation)
    {
        Commands.Remove(allocation.Command);
        CulledData.Remove(allocation.Culled);
        StaticData.Remove(allocation.Static);
        if (allocation.Chunked is { } chunked)
            ChunkedDraws.Remove(chunked);
    }

    public void Clear()
    {
        Commands.Clear();
        CulledData.Clear();
        StaticData.Clear();
        ChunkedDraws.Clear();
    }

    public void Dispose()
    {
        Commands.Dispose();
        CulledData.Dispose();
        StaticData.Dispose();
        ChunkedDraws.Dispose();
    }

    public long Allocated => Commands.Allocated + StaticData.Allocated + CulledData.Allocated + ChunkedDraws.Allocated;
    public long Used => Commands.Used + StaticData.Used + CulledData.Used + ChunkedDraws.Used;
    public BufferStatistics? GetBufferStatistics() => Commands.GetBufferStatistics();
}

public readonly struct DrawAllocation(BufferAllocation command, BufferAllocation @static, BufferAllocation culled, BufferAllocation? chunked)
{
    public readonly BufferAllocation Command = command;
    public readonly BufferAllocation Culled = culled;
    public readonly BufferAllocation Static = @static;
    public readonly BufferAllocation? Chunked = chunked;
}

public readonly struct PerDrawStatic(GeometryHandle geometry, uint sectionId, uint baseMaterial, SectionDescriptor section, uint pickingId, DrawElementsIndirectCommand command, bool castShadow, Vector2 drawDistances, bool outlined)
{
    public readonly uint MeshIndex = geometry.MeshIndex; // index into the per-mesh buffers (PerMeshData, PrimitiveOffsets)
    public readonly uint SectionId = sectionId; // section index in the current model (0-X)
    public readonly uint BaseMaterial = baseMaterial; // offset of the first material this component uses in the material buffer
    public readonly uint PickingId = pickingId;
    public readonly uint OriginalInstanceCount = command.InstanceCount;
    public readonly uint OriginalBaseInstance = command.BaseInstance;
    public readonly uint CastShadow = section.CastShadow && castShadow ? 1u : 0u; // 0 or 1
    public readonly float MinDrawDistance = drawDistances.X;
    public readonly float MaxDrawDistance = drawDistances.Y;
    public readonly uint Outlined = outlined ? 1u : 0u; // 0 or 1

    public static readonly int OriginalInstanceCountOffset = (int)Marshal.OffsetOf<PerDrawStatic>(nameof(OriginalInstanceCount));
    public static readonly int OutlinedOffset = (int)Marshal.OffsetOf<PerDrawStatic>(nameof(Outlined));
}

public readonly struct PerDrawCulled(GeometryHandle geometry, SectionDescriptor section)
{
    public readonly uint Lod; // LOD chosen by the culling pass, so it takes no parameter
    public readonly uint MaterialIndex = section.MaterialIndex; // index of the material relative to BaseMaterial
    public readonly uint BaseColor = geometry.BaseColor; // offset into the vertex color buffer
}
