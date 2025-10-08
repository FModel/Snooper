using CUE4Parse_Conversion.Meshes.PSK;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Descriptors;

public class LodDescriptor<TVertex> where TVertex : unmanaged
{
    public uint IndexCount { get; }
    public uint VertexCount { get; }
    public float ScreenSize { get; }
    public SectionDescriptor[] Sections { get; }

    private TPrimitiveData<TVertex>? _primitive;
    private readonly Func<TPrimitiveData<TVertex>> _factory;
    
    public LodDescriptor(TPrimitiveData<TVertex> primitive)
    {
        _primitive = primitive;

        IndexCount = (uint)(_primitive?.Indices?.Length ?? 0);
        VertexCount = (uint)(_primitive?.Vertices?.Length ?? 0);
        ScreenSize = 0.0f;
        Sections = [new SectionDescriptor(0, IndexCount, 0)];
    }

    public LodDescriptor(CBaseMeshLod lod, Func<CMeshVertex[], uint[], TPrimitiveData<TVertex>> factory)
    {
        var vertices = lod switch
        {
            CStaticMeshLod staticLod => staticLod.Verts,
            CSkelMeshLod skelLod => skelLod.Verts,
            _ => throw new NotSupportedException($"Unsupported mesh type: {lod.GetType().Name}")
        };
        
        if (vertices is not { Length: > 0 })
            throw new ArgumentException("LOD does not contain valid vertices.", nameof(lod));
        if (lod.Indices?.Value is not { Length: > 0 } indices)
            throw new ArgumentException("LOD does not contain valid indices.", nameof(lod));
        if (lod.Sections?.Value is not { Length: > 0 } sections)
            throw new ArgumentException("LOD does not contain valid sections.", nameof(lod));
        
        IndexCount = (uint)indices.Length;
        VertexCount = (uint)vertices.Length;
        ScreenSize = lod.ScreenSize;
        
        Sections = new SectionDescriptor[sections.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            var section = sections[i];
            Sections[i] = new SectionDescriptor((uint)section.FirstIndex, (uint)section.NumFaces * 3, (uint)section.MaterialIndex);
        }
        
        // get rid of this by caching the whole primitive descriptor by guid
        // so we dont convert and set the same mesh values multiple times, we can just reference it
        var capturedVertices = new CMeshVertex[vertices.Length];
        Array.Copy(vertices, capturedVertices, vertices.Length);

        var capturedIndices = new uint[indices.Length];
        Array.Copy(indices, capturedIndices, indices.Length);
        
        _factory = () => factory(capturedVertices, capturedIndices);
    }
    
    internal TPrimitiveData<TVertex> CreatePrimitive()
    {
        if (_primitive != null)
            return _primitive;
            
        _primitive = _factory();
        return _primitive;
    }
}