using System.Numerics;
using CUE4Parse.UE4.Objects.Core.Misc;
using OpenTK.Graphics.OpenGL4;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class GeometryHandle(uint firstIndex, uint baseVertex, BufferAllocation cullingAllocation, uint baseColor, BufferAllocation? boneAllocation, uint baseBoneInfluence, int overrideLod = -1)
{
    public readonly uint FirstIndex = firstIndex; // first index of lod 0
    public readonly uint BaseVertex = baseVertex; // base vertex of lod 0
    public readonly BufferAllocation CullingAllocation = cullingAllocation;
    public readonly BufferAllocation? BoneAllocation = boneAllocation;
    public readonly uint BaseColor = baseColor;
    public readonly uint BaseBoneInfluence = baseBoneInfluence;

    public int OverrideLod { get; internal set; } = overrideLod;
}

public class GeometryPool<TVertex> : IMemoryDetailsProvider, IDisposable where TVertex : unmanaged
{
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<uint> _ebo = new();
    private readonly ArrayBuffer<TVertex> _vbo = new();
    private readonly ShaderStorageBuffer<int> _colors = new();
    private readonly ShaderStorageBuffer<Matrix4x4> _boneData = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluences = new();
    private readonly ShaderStorageBuffer<uint> _boneInfluenceOffsets = new();
    private readonly CullingResources _culling = new();

    private readonly Dictionary<FGuid, GeometryHandle> _cache = new();
    private Action<uint>? _vertexLayoutSetter;

    public void Generate()
    {
        _vao.Generate();
        _ebo.Generate();
        _vbo.Generate();
        _colors.Generate();
        _boneData.Generate();
        _boneInfluences.Generate();
        _boneInfluenceOffsets.Generate();
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
        if (counts.Bones > 0) _boneData.Allocate(counts.Bones);
        if (counts.SkinnedVertices > 0)
        {
            _boneInfluences.Allocate(counts.SkinnedVertices * 2);
            _boneInfluenceOffsets.Allocate(counts.SkinnedVertices);
        }

        _culling.Allocate(counts);
    }

    public GeometryHandle Add(FGuid guid, LodDescriptor<TVertex>[] lods, CullingBounds bounds, SkeletonDescriptor? skeleton = null)
    {
        if (!_cache.TryGetValue(guid, out var handle))
        {
            var (firstIndex, baseVertex, baseColor, baseBoneInfluence, offsets) = CreateOffsets();
            handle = new GeometryHandle(firstIndex, baseVertex, _culling.Add(offsets), baseColor, CreateBoneAllocation(), baseBoneInfluence, lods.Length > 1 ? -1 : 0);
            _cache.Add(guid, handle);
        }

        return handle;

        unsafe (uint, uint, uint, uint, PrimitiveOffsets) CreateOffsets()
        {
            var maxLod = 0u;
            var o = new PrimitiveOffsets(bounds);
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

                if (primitive is { BoneInfluences: { Length: > 0 } boneInfluences, BoneInfluenceCounts: { Length: > 0 } boneInfluenceCounts })
                {
                    var cursor = (uint)_boneInfluences.AddRange(boneInfluences).StartIndex;

                    var packedOffsets = new uint[boneInfluenceCounts.Length];
                    for (var j = 0; j < packedOffsets.Length; j++)
                    {
                        var count = boneInfluenceCounts[j];
                        packedOffsets[j] = (cursor << 8) | count;
                        cursor += count;
                    }

                    o.LOD_BaseBoneInfluence[i] = (uint)_boneInfluenceOffsets.AddRange(packedOffsets).StartIndex;
                }

                maxLod++;
            }
            o.MaxLOD = Math.Min(maxLod, Settings.MaxNumberOfLods) - 1;

            return (o.LOD_FirstIndex[0], o.LOD_BaseVertex[0], o.LOD_BaseColor[0], o.LOD_BaseBoneInfluence[0], o);
        }
        BufferAllocation? CreateBoneAllocation()
        {
            if (skeleton == null) return null;

            var inverseBoneMatrices = new Matrix4x4[skeleton.BoneMatrices.Length];
            for (var i = 0; i < inverseBoneMatrices.Length; i++)
            {
                Matrix4x4.Invert(skeleton.BoneMatrices[i], out inverseBoneMatrices[i]);
            }
            return _boneData.AddRange(inverseBoneMatrices);
        }
    }

    public void Cull<TInstanceData>(IViewProjectionProvider camera, ShaderStorageBuffer<TInstanceData> instances, DrawIndirectBuffer commands, bool shadowPass = false)
        where TInstanceData : unmanaged, IPerInstanceData => _culling.Cull(camera, instances, commands, shadowPass);

    public void Render(Action mdi)
    {
        _boneData.Bind(4);
        _colors.Bind(5);
        _boneInfluences.Bind(6);
        _boneInfluenceOffsets.Bind(7);

        _vao.Bind();
        _ebo.Bind();
        _vbo.Bind();

        mdi.Invoke();

        _vbo.Unbind();
        _ebo.Unbind();
        _vao.Unbind();
    }

    public void UpdateOverrideLod(GeometryHandle handle) => _culling.UpdateOverrideLod(handle.CullingAllocation, handle.OverrideLod);

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
        _boneData.Dispose();
        _boneInfluences.Dispose();
        _boneInfluenceOffsets.Dispose();
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
            total += _boneData.Allocated;
            total += _boneInfluences.Allocated;
            total += _boneInfluenceOffsets.Allocated;
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
            total += _boneData.Used;
            total += _boneInfluences.Used;
            total += _boneInfluenceOffsets.Used;
            total += _culling.Used;
            return total;
        }
    }

    public IEnumerable<MemoryDetail> GetMemoryDetails()
    {
        yield return new MemoryDetail("Index Buffer", _ebo);
        yield return new MemoryDetail("Vertex Buffer", _vbo);
        yield return new MemoryDetail("Vertex Color Buffer", _colors);
        yield return new MemoryDetail("Bone Data", _boneData);
        yield return new MemoryDetail("Bone Influence Buffer", _boneInfluences);
        yield return new MemoryDetail("Bone Influence Offset Buffer", _boneInfluenceOffsets);
        yield return new MemoryDetail("Culling Resources", _culling);
    }
}
