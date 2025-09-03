using System.Text;
using CUE4Parse.UE4.Objects.Core.Misc;
using Snooper.Core.Containers.Buffers;
using Snooper.Rendering.Components.Camera;
using Snooper.Rendering.Components.Primitive;

namespace Snooper.Core.Containers.Resources;

public struct GeometryHandle(uint firstIndex, uint baseVertex, uint modelId)
{
    public readonly uint FirstIndex = firstIndex;
    public readonly uint BaseVertex = baseVertex;
    public readonly uint ModelId = modelId;
}

public class GeometryPool<TVertex>(int initialDrawCapacity) : IDisposable, IMemorySizeProvider where TVertex : unmanaged
{
    private readonly VertexArray _vao = new();
    private readonly ElementArrayBuffer<uint> _ebo = new(initialDrawCapacity * 2000);
    private readonly ArrayBuffer<TVertex> _vbo = new(initialDrawCapacity * 1000);
    private readonly CullingResources _culling = new(initialDrawCapacity);
    
    private readonly Dictionary<FGuid, GeometryHandle> _cache = new();

    public void Generate()
    {
        _vao.Generate();
        _ebo.Generate();
        _vbo.Generate();
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
    
    public void Allocate(int componentCount, int drawCount, int indices, int vertices)
    {
        _ebo.Bind();
        _ebo.Allocate(new uint[indices]);
        _ebo.Unbind();
        
        _vbo.Bind();
        _vbo.Allocate(new TVertex[vertices]);
        _vbo.Unbind();
        
        _culling.Allocate(componentCount, drawCount);
    }
    
    public GeometryHandle Add(FGuid guid, Func<LevelOfDetail<TVertex>[]> factory, CullingBounds bounds)
    {
        if (!_cache.TryGetValue(guid, out var handle))
        {
            var (firstIndex, baseVertex, descriptor) = CreateDescriptor(factory());
            handle = new GeometryHandle(firstIndex, baseVertex, (uint)_culling.Add(descriptor));
            _cache.Add(guid, handle);
        }
        
        return handle;

        unsafe (uint, uint, PrimitiveDescriptor) CreateDescriptor(LevelOfDetail<TVertex>[] lods)
        {
            _ebo.Bind();
            _vbo.Bind();
            
            var maxLod = 0u;
            var d = new PrimitiveDescriptor(bounds);
            for (var i = 0; i < lods.Length && i < Settings.MaxNumberOfLods; i++)
            {
                if (!lods[i].Primitive.IsValid)
                {
                    continue;
                    // throw new InvalidOperationException("Primitive data is not valid.");
                }
                
                d.LOD_FirstIndex[i] = (uint)_ebo.AddRange(lods[i].Primitive.Indices);
                d.LOD_BaseVertex[i] = (uint)_vbo.AddRange(lods[i].Primitive.Vertices);
                d.LOD_ScreenSize[i] = lods[i].ScreenSize;
                d.LOD_SectionCount[i] = (uint)lods[i].SectionDescriptors.Length;
                d.LOD_SectionOffset[i] = (uint)_culling.Add(lods[i].SectionDescriptors);
                
                maxLod++;
                lods[i].Dispose();
            }
            d.Bounds.MaxLevelOfDetail = Math.Min(maxLod, Settings.MaxNumberOfLods) - 1;

            _vbo.Unbind();
            _ebo.Unbind();
            
            return (d.LOD_FirstIndex[0], d.LOD_BaseVertex[0], d);
        }
    }
    
    public void Cull<TInstanceData>(CameraComponent camera, ShaderStorageBuffer<TInstanceData> instances, DrawIndirectBuffer commands)
        where TInstanceData : unmanaged, IPerInstanceData => _culling.Cull(camera, instances, commands);
    
    public void Render(Action mdi)
    {
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
        _culling.Dispose();
    }

    public string GetFormattedSpace()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"    x{_ebo.Count} Indices:      {_ebo.GetFormattedSpace()}");
        builder.AppendLine($"    x{_vbo.Count} Vertices:     {_vbo.GetFormattedSpace()}");
        return builder.ToString();
    }
}