using System.Diagnostics;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Containers.Programs;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class CullingResources : IMemoryDetailsProvider, IDisposable
{
    private readonly ShaderStorageBuffer<PerMeshData> _meshes = new();
    private readonly ShaderStorageBuffer<PrimitiveOffsets> _primitives = new();
    private readonly ShaderStorageBuffer<SectionOffsets> _sections = new();
    private const int GroupSize = 64;

    private static readonly string[] _computeDefines =
    [
        $"MAX_CULLING_VIEWS {Settings.MaxCullingViews}",
        $"MAX_INSTANCES_PER_THREAD {Settings.MaxInstancesPerCullThread}",
        $"CULL_GROUP_SIZE {GroupSize}",
        $"CHUNKED_DRAW_OFFSET {IndirectDrawBuffer.ChunkedDrawOffset}u",
        .. CullingBindings.OwnDefines
    ];

    private readonly ComputeShader _compute = new("culling.comp") // fast path for draws with low instance counts, everything about thread sharing is stripped out
    {
        Defines = _computeDefines
    };
    private readonly ComputeShader _chunkedCompute = new("culling.comp") // for draws with high instance counts
    {
        Defines = [.._computeDefines, "CULL_CHUNKED"]
    };

    private abstract class CullingBindings : Bindings
    {
        public const uint DrawCommands = BaseMaxBinding + 1;
        public const uint CullLodData = BaseMaxBinding + 2;
        public const uint CullSections = BaseMaxBinding + 3;
        public const uint CullChunked = BaseMaxBinding + 4;
        public const uint MaxBinding = CullChunked;

        public static readonly string[] OwnDefines =
        [
            Define("DRAW_COMMANDS", DrawCommands),
            Define("CULL_LOD_DATA", CullLodData),
            Define("CULL_SECTIONS", CullSections),
            Define("CULL_CHUNKED", CullChunked)
        ];
    }

    public void Generate()
    {
        _meshes.Generate();
        _primitives.Generate();
        _sections.Generate();

        _compute.Generate();
        _compute.Link();
        _chunkedCompute.Generate();
        _chunkedCompute.Link();
    }

    public void Allocate(AllocationCounts counts)
    {
        if (counts.UniqueComponents > 0)
        {
            _meshes.Allocate(counts.UniqueComponents);
            _primitives.Allocate(counts.UniqueComponents);
        }
        if (counts.Sections > 0) _sections.Allocate(counts.Sections);
    }

    public BufferAllocation Add(SectionDescriptor[] sections)
    {
        var offsets = new SectionOffsets[sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            offsets[i] = new SectionOffsets(sections[i]);
        }

        return _sections.AddRange(offsets);
    }

    public (BufferAllocation Mesh, BufferAllocation Primitives) Add(PerMeshData mesh, PrimitiveOffsets lods)
    {
        var meshAllocation = _meshes.Add(mesh);
        var lodAllocation = _primitives.Add(lods);
        Debug.Assert(meshAllocation.StartIndex == lodAllocation.StartIndex, "PerMeshData and PrimitiveOffsets buffers must stay index-aligned.");
        return (meshAllocation, lodAllocation);
    }

    public void UpdateOverrideLod(BufferAllocation allocation, int overrideLod)
    {
        _meshes.UpdateCustom(allocation, overrideLod, PerMeshData.OverrideLodOffset);
    }

    public void BindMeshData() => _meshes.Bind(Bindings.MeshData);

    private readonly Plane[] _planes = new Plane[Settings.MaxCullingViews * 6];
    private readonly Vector4[] _lodReferences = new Vector4[Settings.MaxCullingViews];
    private readonly float[] _lodOrthoExtents = new float[Settings.MaxCullingViews];

    public void Cull<TInstanceData>(ReadOnlySpan<CullView> views, ShaderStorageBuffer<TInstanceData> instances, IndirectDrawBuffer commands) where TInstanceData : unmanaged, IPerInstanceData
    {
        var viewCount = Math.Min(views.Length, commands.MaskViewIndex);
        if (commands.Extent == 0) return;

        for (var i = 0; i < viewCount; i++)
        {
            var matrix = views[i].ViewProjection;
            var b = i * 6;
            _planes[b + 0] = new Plane(matrix.M14 + matrix.M11, matrix.M24 + matrix.M21, matrix.M34 + matrix.M31, matrix.M44 + matrix.M41); // Left
            _planes[b + 1] = new Plane(matrix.M14 - matrix.M11, matrix.M24 - matrix.M21, matrix.M34 - matrix.M31, matrix.M44 - matrix.M41); // Right
            _planes[b + 2] = new Plane(matrix.M14 + matrix.M12, matrix.M24 + matrix.M22, matrix.M34 + matrix.M32, matrix.M44 + matrix.M42); // Bottom
            _planes[b + 3] = new Plane(matrix.M14 - matrix.M12, matrix.M24 - matrix.M22, matrix.M34 - matrix.M32, matrix.M44 - matrix.M42); // Top
            _planes[b + 4] = new Plane(matrix.M13, matrix.M23, matrix.M33, matrix.M43); // z >= 0
            _planes[b + 5] = new Plane(matrix.M14 - matrix.M13, matrix.M24 - matrix.M23, matrix.M34 - matrix.M33, matrix.M44 - matrix.M43); // w - z >= 0

            _lodReferences[i] = new Vector4(views[i].LodReferencePosition, views[i].LodProjectionScale);
            _lodOrthoExtents[i] = views[i].LodOrthoExtent;
        }

        commands.Commands.Bind(CullingBindings.DrawCommands);
        commands.StaticData.Bind(Bindings.DrawStatic);
        commands.CulledData.Bind(Bindings.DrawCulled);
        instances.Bind(Bindings.InstanceData);
        BindMeshData();
        _primitives.Bind(CullingBindings.CullLodData);
        _sections.Bind(CullingBindings.CullSections);

        Dispatch(_compute, (commands.Extent + GroupSize - 1) / GroupSize);
        if (commands.ChunkedDraws.Extent > 0)
        {
            commands.ChunkedDraws.Bind(CullingBindings.CullChunked);
            Dispatch(_chunkedCompute, commands.ChunkedDraws.Extent);
        }

        GL.MemoryBarrier(MemoryBarrierFlags.CommandBarrierBit | MemoryBarrierFlags.ShaderStorageBarrierBit);
        return;

        void Dispatch(ComputeShader compute, int groups)
        {
            compute.Use();
            compute.SetUniform("uFrustumPlanes", _planes);
            compute.SetUniform("uLodReference", _lodReferences);
            compute.SetUniform("uLodOrthoExtent", _lodOrthoExtents);
            compute.SetUniform("uViewCount", (uint) viewCount);
            compute.SetUniform("uMaskView", (uint) commands.MaskViewIndex);
            compute.SetUniform("uViewCapacity", (uint) commands.Capacity);

            GL.DispatchCompute(groups, commands.ViewCount, 1);
            compute.Unuse();
        }
    }

    public void Remove(BufferAllocation mesh, BufferAllocation primitives, List<BufferAllocation> sections)
    {
        _meshes.Remove(mesh);
        _primitives.Remove(primitives);
        foreach (var allocation in sections)
            _sections.Remove(allocation);
    }

    public void Dispose()
    {
        _meshes.Dispose();
        _primitives.Dispose();
        _sections.Dispose();
        _compute.Dispose();
        _chunkedCompute.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _meshes.Allocated;
            total += _primitives.Allocated;
            total += _sections.Allocated;
            total += _compute.Allocated;
            total += _chunkedCompute.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _meshes.Used;
            total += _primitives.Used;
            total += _sections.Used;
            total += _compute.Used;
            total += _chunkedCompute.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Mesh Data", _meshes);
        yield return new MemoryDetail("Primitive Offsets", _primitives);
        yield return new MemoryDetail("Section Offsets", _sections);
        yield return new MemoryDetail("Culling Compute Shader", _compute);
        yield return new MemoryDetail("Chunked Culling Compute Shader", _chunkedCompute);
    }
}
