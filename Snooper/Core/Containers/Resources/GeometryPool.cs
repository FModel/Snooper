using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class GeometryHandle(uint firstIndex, uint baseVertex, BufferAllocation meshAllocation, uint baseColor, int overrideLod = -1)
{
    public readonly uint FirstIndex = firstIndex; // first index of lod 0
    public readonly uint BaseVertex = baseVertex; // base vertex of lod 0
    public readonly BufferAllocation MeshAllocation = meshAllocation; // one entry per unique mesh in both the mesh data and per-lod buffers
    public readonly uint BaseColor = baseColor;

    public uint MeshIndex => (uint)MeshAllocation.StartIndex;
    public int OverrideLod { get; internal set; } = overrideLod;
}

public class GeometryPool<TVertex> : IMemoryDetailsProvider, IDisposable where TVertex : unmanaged
{
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<uint> _ebo = new();
    private readonly ArrayBuffer<TVertex> _vbo = new();
    private readonly ShaderStorageBuffer<int> _colors = new();
    private readonly CullingResources _culling = new();

    private readonly Dictionary<FGuid, GeometryHandle> _cache = new();
    private Action<uint>? _vertexLayoutSetter;

    public void Generate()
    {
        _vao.Generate();
        _ebo.Generate();
        _vbo.Generate();
        _colors.Generate();
        _culling.Generate();

        _ebo.OnHandleChanged += (_, _) => BindBuffersToVao();
        _vbo.OnHandleChanged += (_, _) => BindBuffersToVao();
    }

    public void SetVertexLayout(Action<uint> setter)
    {
        _vertexLayoutSetter = setter;
        BindBuffersToVao();
    }

    private void BindBuffersToVao()
    {
        GL.VertexArrayVertexBuffer(_vao, 0, _vbo, 0, _vbo.Stride);
        GL.VertexArrayElementBuffer(_vao, _ebo);

        _vertexLayoutSetter?.Invoke(_vao);
    }

    public void Allocate(AllocationCounts counts)
    {
        if (counts.Indices > 0) _ebo.Allocate(counts.Indices);
        if (counts.Vertices > 0) _vbo.Allocate(counts.Vertices);
        if (counts.ColoredVertices > 0) _colors.Allocate(counts.ColoredVertices);

        _culling.Allocate(counts);
    }

    public GeometryHandle Add(PrimitiveDescriptor<TVertex> descriptor, Vector2 drawDistances)
    {
        var lods = descriptor.Lods;

        if (!_cache.TryGetValue(descriptor.Guid, out var handle))
        {
            var (firstIndex, baseVertex, baseColor, maxLod, offsets) = CreateOffsets();
            var mesh = new PerMeshData(descriptor.Bounds, maxLod, drawDistances);
            handle = new GeometryHandle(firstIndex, baseVertex, _culling.Add(mesh, offsets), baseColor, lods.Length > 1 ? -1 : 0);
            _cache.Add(descriptor.Guid, handle);
        }

        return handle;

        unsafe (uint, uint, uint, uint, PrimitiveOffsets) CreateOffsets()
        {
            var maxLod = 0u;
            var o = new PrimitiveOffsets();
            for (var i = 0; i < lods.Length && i < Settings.MaxNumberOfLods; i++)
            {
                var primitive = lods[i].CreatePrimitive();
                if (primitive.Vertices is not { Length: > 0 } || primitive.Indices is not { Length: > 0 })
                {
                    continue;
                    // throw new InvalidOperationException("Primitive data is not valid.");
                }

                o.LOD_FirstIndex[i] = (uint)_ebo.AddRange(primitive.Indices).StartIndex;
                o.LOD_BaseVertex[i] = (uint)_vbo.AddRange(primitive.Vertices).StartIndex;
                o.LOD_ScreenSize[i] = lods[i].ScreenSize;
                o.LOD_SectionCount[i] = (uint)lods[i].Sections.Length;
                o.LOD_SectionOffset[i] = (uint)_culling.Add(lods[i].Sections).StartIndex;

                if (primitive.Colors is { Length: > 0 } colors)
                {
                    o.LOD_BaseColor[i] = (uint)_colors.AddRange(colors).StartIndex;
                }

                maxLod++;
            }

            return (o.LOD_FirstIndex[0], o.LOD_BaseVertex[0], o.LOD_BaseColor[0], Math.Min(maxLod, Settings.MaxNumberOfLods) - 1, o);
        }
    }

    public void Cull<TInstanceData>(IViewProjectionProvider camera, ShaderStorageBuffer<TInstanceData> instances, IndirectDrawBuffer commands, bool shadowPass = false)
        where TInstanceData : unmanaged, IPerInstanceData => _culling.Cull(camera, instances, commands, shadowPass);

    public void Render(Action mdi)
    {
        _colors.Bind(Bindings.VertexColors);

        _vao.Bind();
        _ebo.Bind();
        _vbo.Bind();

        mdi.Invoke();

        _vbo.Unbind();
        _ebo.Unbind();
        _vao.Unbind();
    }

    public void UpdateOverrideLod(GeometryHandle handle) => _culling.UpdateOverrideLod(handle.MeshAllocation, handle.OverrideLod);

    public void Remove(GeometryHandle handle)
    {
        // TODO: do this properly
        // we need to keep track of all allocations made for this handle
        // + this whole thing is cached, so we need to remove the handle only if it's the last reference
    }

    public void Dispose()
    {
        _vao.Dispose();
        _ebo.Dispose();
        _vbo.Dispose();
        _colors.Dispose();
        _culling.Dispose();
    }

    public long Allocated
    {
        get
        {
            long total = 0;
            total += _ebo.Allocated;
            total += _vbo.Allocated;
            total += _colors.Allocated;
            total += _culling.Allocated;
            return total;
        }
    }

    public long Used
    {
        get
        {
            long total = 0;
            total += _ebo.Used;
            total += _vbo.Used;
            total += _colors.Used;
            total += _culling.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Index Buffer", _ebo);
        yield return new MemoryDetail("Vertex Buffer", _vbo);
        yield return new MemoryDetail("Vertex Color Buffer", _colors);
        yield return new MemoryDetail("Culling Resources", _culling);
    }
}
