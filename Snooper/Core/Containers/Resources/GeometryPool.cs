using System.Numerics;
using System.Text;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Descriptors;

namespace Snooper.Core.Containers.Resources;

public class GeometryHandle(uint firstIndex, uint baseVertex, uint baseGeometry, uint baseColor)
{
    public readonly uint FirstIndex = firstIndex; // first index of lod 0
    public readonly uint BaseVertex = baseVertex; // base vertex of lod 0
    public readonly uint BaseGeometry = baseGeometry;
    public readonly uint BaseColor = baseColor;
    
    private int _overrideLod = -1;
    public int OverrideLod
    {
        get => _overrideLod;
        internal set
        {
            if (_overrideLod == value) return;
            
            _overrideLod = value;
            IsDirty = true;
        }
    }
    
    public bool IsDirty { get; private set; }
    
    public void MarkClean() => IsDirty = false;
}

public class GeometryPool<TVertex>(int initialDrawCapacity) : IDisposable, IMemorySizeProvider where TVertex : unmanaged
{
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<uint> _ebo = new(initialDrawCapacity);
    private readonly ArrayBuffer<TVertex> _vbo = new(initialDrawCapacity);
    private readonly ShaderStorageBuffer<int> _colors = new(initialDrawCapacity);
    private readonly CullingResources _culling = new(initialDrawCapacity);
    
    private readonly Dictionary<FGuid, GeometryHandle> _cache = new();

    public void Generate()
    {
        _vao.Generate();
        _ebo.Generate();
        _vbo.Generate();
        _colors.Generate();
        _culling.Generate();
    }

    public void SetVertexLayout(Action<int> setter)
    {
        _vao.Bind();
        
        _vbo.Bind();
        setter.Invoke(_vbo.Stride);
        _vbo.Unbind();

        _vao.Unbind();
    }
    
    public void Allocate(AllocationCounts counts)
    {
        _ebo.Bind();
        _ebo.Allocate(new uint[counts.Indices]);
        _ebo.Unbind();
        
        _vbo.Bind();
        _vbo.Allocate(new TVertex[counts.Vertices]);
        _vbo.Unbind();

        if (counts.ColoredVertices > 0)
        {
            _colors.Bind();
            _colors.Allocate(new int[counts.ColoredVertices]);
            _colors.Unbind();
        }

        _culling.Allocate(counts);
    }
    
    public GeometryHandle Add(FGuid guid, LodDescriptor<TVertex>[] lods, CullingBounds bounds)
    {
        if (!_cache.TryGetValue(guid, out var handle))
        {
            var (firstIndex, baseVertex, baseColor, offsets) = CreateOffsets();
            handle = new GeometryHandle(firstIndex, baseVertex, (uint)_culling.Add(offsets), baseColor);
            _cache.Add(guid, handle);
        }
        
        return handle;

        unsafe (uint, uint, uint, PrimitiveOffsets) CreateOffsets()
        {
            _ebo.Bind();
            _vbo.Bind();
            
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
                
                o.LOD_FirstIndex[i] = (uint)_ebo.AddRange(primitive.Indices);
                o.LOD_BaseVertex[i] = (uint)_vbo.AddRange(primitive.Vertices);
                o.LOD_ScreenSize[i] = lods[i].ScreenSize;
                o.LOD_SectionCount[i] = (uint)lods[i].Sections.Length;
                o.LOD_SectionOffset[i] = (uint)_culling.Add(lods[i].Sections);
                
                if (primitive.Colors != null)
                {
                    _colors.Bind();
                    o.LOD_BaseColor[i] = (uint)_colors.AddRange(primitive.Colors);
                    _colors.Unbind();
                }

                maxLod++;
            }
            o.MaxLOD = Math.Min(maxLod, Settings.MaxNumberOfLods) - 1;

            _vbo.Unbind();
            _ebo.Unbind();
            
            return (o.LOD_FirstIndex[0], o.LOD_BaseVertex[0], o.LOD_BaseColor[0], o);
        }
    }
    
    public void Cull<TInstanceData>(CameraComponent camera, ShaderStorageBuffer<TInstanceData> instances, DrawIndirectBuffer commands)
        where TInstanceData : unmanaged, IPerInstanceData => _culling.Cull(camera, instances, commands);
    
    public void UpdateOverrideLod(int index, int overrideLod) => _culling.UpdateOverrideLod(index, overrideLod);
    
    public void Render(Action mdi)
    {
        _colors.Bind(5);
        
        _vao.Bind();
        _ebo.Bind();
        
        mdi.Invoke();
        
        _ebo.Unbind();
        _vao.Unbind();
    }

    public void Dispose()
    {
        _vao.Dispose();
        _ebo.Dispose();
        _vbo.Dispose();
        _colors.Dispose();
        _culling.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"    x{_ebo.Count} Indices:      {_ebo.GetFormattedSpace()}");
        builder.AppendLine($"    x{_vbo.Count} Vertices:     {_vbo.GetFormattedSpace()}");
        builder.AppendLine($"    x{_colors.Count} Colors:       {_colors.GetFormattedSpace()}");
        return builder.ToString();
    }
}