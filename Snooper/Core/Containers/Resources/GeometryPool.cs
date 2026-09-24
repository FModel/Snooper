using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Core.Hardware;
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

public readonly struct VertexArrayLayout
{
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _stride;

    public VertexArrayLayout(uint vao, uint vbo, int stride)
    {
        _vao = vao;
        _vbo = vbo;
        _stride = stride;

        if (!DeviceInfo.IsIntel)
        {
            GL.VertexArrayVertexBuffer(vao, 0, vbo, 0, stride);
        }
    }

    public VertexArrayLayout Float(uint location, int size, VertexAttribType type = VertexAttribType.Float, bool normalized = false, uint offset = 0)
    {
        GL.VertexArrayAttribFormat(_vao, location, size, type, normalized, DeviceInfo.IsIntel ? 0 : offset);
        return Enable(location, offset);
    }

    public VertexArrayLayout Integer(uint location, int size, VertexAttribIType type = VertexAttribIType.UnsignedInt, uint offset = 0)
    {
        GL.VertexArrayAttribIFormat(_vao, location, size, type, DeviceInfo.IsIntel ? 0 : offset);
        return Enable(location, offset);
    }

    private VertexArrayLayout Enable(uint location, uint offset)
    {
        var binding = 0u;
        if (DeviceInfo.IsIntel)
        {
            binding = location;
            GL.VertexArrayVertexBuffer(_vao, binding, _vbo, (nint)offset, _stride);
        }

        GL.VertexArrayAttribBinding(_vao, location, binding);
        GL.EnableVertexArrayAttrib(_vao, location);
        return this;
    }
}

public sealed class CachedGeometry : IMemorySizeProvider
{
    public readonly string Name;
    public readonly FGuid Guid;
    public readonly GeometryHandle Handle;

    internal readonly BufferAllocation _primitives;
    internal readonly List<BufferAllocation> _indices;
    internal readonly List<BufferAllocation> _vertices;
    internal readonly List<BufferAllocation> _colors;
    internal readonly List<BufferAllocation> _sections;

    public int RefCount { get; internal set; }
    public int LodCount => _indices.Count;
    public int GetIndexCount(int lod) => _indices[lod].Length;
    public int GetVertexCount(int lod) => _vertices[lod].Length;

    public long Allocated { get; }
    public long Used => Allocated;

    public CachedGeometry(string name, FGuid guid, GeometryHandle handle, BufferAllocation primitives,
        List<BufferAllocation> indices, List<BufferAllocation> vertices, List<BufferAllocation> colors,
        List<BufferAllocation> sections, int indexStride, int vertexStride, int colorStride)
    {
        Name = name;
        Guid = guid;
        Handle = handle;

        _primitives = primitives;
        _indices = indices;
        _vertices = vertices;
        _colors = colors;
        _sections = sections;

        RefCount = 1;
        Allocated =
            indices.Sum(x => (long) x.Length) * indexStride +
            vertices.Sum(x => (long) x.Length) * vertexStride +
            colors.Sum(x => (long) x.Length) * colorStride;
    }
}

public class GeometryPool<TVertex> : IMemoryDetailsProvider, IDisposable where TVertex : unmanaged
{
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<uint> _ebo = new();
    private readonly ArrayBuffer<TVertex> _vbo = new();
    private readonly ShaderStorageBuffer<int> _colors = new();
    private readonly CullingResources _culling = new();

    private readonly Dictionary<FGuid, CachedGeometry> _cache = new();
    private Action<VertexArrayLayout>? _vertexLayoutSetter;

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

    public void SetVertexLayout(Action<VertexArrayLayout> setter)
    {
        _vertexLayoutSetter = setter;
        BindBuffersToVao();
    }

    private void BindBuffersToVao()
    {
        GL.VertexArrayElementBuffer(_vao, _ebo);
        _vertexLayoutSetter?.Invoke(new VertexArrayLayout(_vao, _vbo, _vbo.Stride));
    }

    public void Allocate(AllocationCounts counts)
    {
        if (counts.Indices > 0) _ebo.Allocate(counts.Indices);
        if (counts.Vertices > 0) _vbo.Allocate(counts.Vertices);
        if (counts.ColoredVertices > 0) _colors.Allocate(counts.ColoredVertices);

        _culling.Allocate(counts);
    }

    public GeometryHandle Add(PrimitiveDescriptor<TVertex> descriptor)
    {
        var lods = descriptor.Lods;

        if (_cache.TryGetValue(descriptor.Guid, out var cached))
        {
            cached.RefCount++;
            return cached.Handle;
        }

        var indices = new List<BufferAllocation>();
        var vertices = new List<BufferAllocation>();
        var colors = new List<BufferAllocation>();
        var sections = new List<BufferAllocation>();

        var (firstIndex, baseVertex, baseColor, maxLod, offsets) = CreateOffsets();
        var mesh = new PerMeshData(descriptor.Bounds, maxLod, descriptor.ColorMode);
        var (meshAllocation, primitivesAllocation) = _culling.Add(mesh, offsets);
        var handle = new GeometryHandle(firstIndex, baseVertex, meshAllocation, baseColor, lods.Length > 1 ? -1 : 0);
        _cache.Add(descriptor.Guid, new CachedGeometry(descriptor.Name ?? Settings.NoName, descriptor.Guid, handle, primitivesAllocation, indices, vertices, colors, sections, _ebo.Stride, _vbo.Stride, _colors.Stride));

        return handle;

        unsafe (uint, uint, uint, uint, PrimitiveOffsets) CreateOffsets()
        {
            var maxLod = 0u;
            var o = new PrimitiveOffsets();
            for (var i = 0; i < lods.Length && i < Settings.MaxNumberOfLods; i++)
            {
                var primitive = lods[i].CreatePrimitive(); // cached, already created by MeshComponent.BeginPlay
                if (primitive.Vertices is not { Length: > 0 } || primitive.Indices is not { Length: > 0 })
                {
                    continue;
                    // throw new InvalidOperationException("Primitive data is not valid.");
                }

                var indexAllocation = _ebo.AddRange(primitive.Indices);
                var vertexAllocation = _vbo.AddRange(primitive.Vertices);
                var sectionAllocation = _culling.Add(lods[i].Sections);
                indices.Add(indexAllocation);
                vertices.Add(vertexAllocation);
                sections.Add(sectionAllocation);

                o.LOD_FirstIndex[i] = (uint)indexAllocation.StartIndex;
                o.LOD_BaseVertex[i] = (uint)vertexAllocation.StartIndex;
                o.LOD_ScreenSize[i] = lods[i].ScreenSize;
                o.LOD_SectionCount[i] = (uint)lods[i].Sections.Length;
                o.LOD_SectionOffset[i] = (uint)sectionAllocation.StartIndex;

                if (primitive.Colors is { Length: > 0 })
                {
                    var colorAllocation = _colors.AddRange(primitive.Colors);
                    colors.Add(colorAllocation);
                    o.LOD_BaseColor[i] = (uint)colorAllocation.StartIndex;
                }

                maxLod++;
            }

            var lodCount = Math.Min(maxLod, Settings.MaxNumberOfLods);
            return (o.LOD_FirstIndex[0], o.LOD_BaseVertex[0], o.LOD_BaseColor[0], lodCount > 0 ? lodCount - 1 : 0, o);
        }
    }

    public void Cull<TInstanceData>(ReadOnlySpan<CullView> views, ShaderStorageBuffer<TInstanceData> instances, IndirectDrawBuffer commands)
        where TInstanceData : unmanaged, IPerInstanceData => _culling.Cull(views, instances, commands);

    public void Render(Action mdi)
    {
        _colors.Bind(Bindings.VertexColors);
        _culling.BindMeshData();

        _vao.Bind();
        _ebo.Bind();
        _vbo.Bind();

        mdi.Invoke();

        _vbo.Unbind();
        _ebo.Unbind();
        _vao.Unbind();
    }

    public void UpdateOverrideLod(GeometryHandle handle) => _culling.UpdateOverrideLod(handle.MeshAllocation, handle.OverrideLod);

    public void Remove(FGuid guid)
    {
        if (!_cache.TryGetValue(guid, out var cached) || --cached.RefCount > 0) return;

        _cache.Remove(guid);
        foreach (var allocation in cached._indices) _ebo.Remove(allocation);
        foreach (var allocation in cached._vertices) _vbo.Remove(allocation);
        foreach (var allocation in cached._colors) _colors.Remove(allocation);
        _culling.Remove(cached.Handle.MeshAllocation, cached._primitives, cached._sections);
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
