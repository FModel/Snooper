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

    public readonly int ViewCount = viewCount;
    public int Capacity => Commands.Capacity;
    public int Stride => Commands.Stride;

    public void Generate()
    {
        Commands.Generate();
        CulledData.Generate();
        StaticData.Generate();
    }

    public void Allocate(uint size)
    {
        Commands.Allocate(size);
        CulledData.Allocate(size);
        StaticData.Allocate(size);
    }

    public DrawAllocation Add(DrawElementsIndirectCommand command, PerDrawStatic data, PerDrawCulled output)
    {
        var commandAllocation = Commands.Add(command);
        var dataAllocation = StaticData.Add(data);
        var outputAllocation = CulledData.Add(output);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex && commandAllocation.StartIndex == outputAllocation.StartIndex,
            "Draw command, per-draw data and per-draw output buffers must stay index-aligned.");
        return new DrawAllocation(commandAllocation, dataAllocation, outputAllocation);
    }

    public DrawAllocation CopyFrom(IndirectDrawBuffer source, DrawAllocation allocation)
    {
        var commandAllocation = Commands.CopyFrom(source.Commands, allocation.Command);
        var dataAllocation = StaticData.CopyFrom(source.StaticData, allocation.Static);
        var outputAllocation = CulledData.CopyFrom(source.CulledData, allocation.Culled);
        Debug.Assert(commandAllocation.StartIndex == dataAllocation.StartIndex && commandAllocation.StartIndex == outputAllocation.StartIndex,
            "Draw command, per-draw data and per-draw output buffers must stay index-aligned.");
        return new DrawAllocation(commandAllocation, dataAllocation, outputAllocation);
    }

    public int GetViewBase(int view) => (view < ViewCount ? view : 0) * Capacity;
    public nint GetViewOffset(int view) => GetViewBase(view) * Stride;

    public void Remove(DrawAllocation allocation)
    {
        Commands.Remove(allocation.Command);
        CulledData.Remove(allocation.Culled);
        StaticData.Remove(allocation.Static);
    }

    public void Clear()
    {
        Commands.Clear();
        CulledData.Clear();
        StaticData.Clear();
    }

    public readonly struct DeferMergeScope(IndirectDrawBuffer buffer) : IDisposable
    {
        private readonly Buffer<DrawElementsIndirectCommand>.DeferMergeScope _commandScope = buffer.Commands.DeferMerge();
        private readonly Buffer<PerDrawCulled>.DeferMergeScope _outputScope = buffer.CulledData.DeferMerge();
        private readonly Buffer<PerDrawStatic>.DeferMergeScope _dataScope = buffer.StaticData.DeferMerge();

        public void Dispose()
        {
            _commandScope.Dispose();
            _outputScope.Dispose();
            _dataScope.Dispose();
        }
    }

    public DeferMergeScope DeferMerge() => new(this);

    public void Dispose()
    {
        Commands.Dispose();
        CulledData.Dispose();
        StaticData.Dispose();
    }

    public long Allocated => Commands.Allocated + StaticData.Allocated + CulledData.Allocated;
    public long Used => Commands.Used + StaticData.Used + CulledData.Used;
    public BufferStatistics? GetBufferStatistics() => Commands.GetBufferStatistics();
}

public readonly struct DrawAllocation(BufferAllocation command, BufferAllocation @static, BufferAllocation culled)
{
    public readonly BufferAllocation Command = command;
    public readonly BufferAllocation Culled = culled;
    public readonly BufferAllocation Static = @static;
}

public readonly struct PerDrawStatic(GeometryHandle geometry, uint sectionId, uint baseMaterial, SectionDescriptor section, uint pickingId, DrawElementsIndirectCommand command, bool castShadow, Vector2 drawDistances)
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

    public static readonly int OriginalInstanceCountOffset = (int)Marshal.OffsetOf<PerDrawStatic>(nameof(OriginalInstanceCount));
}

public readonly struct PerDrawCulled(GeometryHandle geometry, SectionDescriptor section)
{
    public readonly uint Lod; // LOD chosen by the culling pass, so it takes no parameter
    public readonly uint MaterialIndex = section.MaterialIndex; // index of the material relative to BaseMaterial
    public readonly uint BaseColor = geometry.BaseColor; // offset into the vertex color buffer
}
