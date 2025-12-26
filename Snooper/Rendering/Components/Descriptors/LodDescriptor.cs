using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Meshes;
using Snooper.Rendering.Primitives;

namespace Snooper.Rendering.Components.Descriptors;

public class LodDescriptor<TVertex> : IDisposable where TVertex : unmanaged
{
    public uint IndexCount { get; }
    public uint VertexCount { get; }
    public float ScreenSize { get; }
    public uint LayerCount { get; }
    public bool HasVertexColors { get; }
    public SectionDescriptor[] Sections { get; }

    private TPrimitiveData<TVertex>? _primitive;
    private readonly Func<TPrimitiveData<TVertex>>? _factory;

    public LodDescriptor(TPrimitiveData<TVertex> primitive)
    {
        _primitive = primitive;
        _factory = null;

        IndexCount = (uint)(_primitive?.Indices?.Length ?? 0);
        VertexCount = (uint)(_primitive?.Vertices?.Length ?? 0);
        ScreenSize = 0.0f;
        LayerCount = 1;
        HasVertexColors = false;
        Sections = [new SectionDescriptor(0, IndexCount, 0)];
    }

    public LodDescriptor(CBaseMeshLod lod, Func<CMeshVertex[], uint[], FColor[]?, FMeshUVFloat[]?, TPrimitiveData<TVertex>> factory)
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
        LayerCount = (uint)Math.Max(1, lod.NumTexCoords);

        Sections = new SectionDescriptor[sections.Length];
        for (var i = 0; i < Sections.Length; i++)
        {
            var section = sections[i];
            Sections[i] = new SectionDescriptor((uint)section.FirstIndex, (uint)section.NumFaces * 3, (uint)section.MaterialIndex, section.CastShadow, section.MaterialName);
        }

        // capture vertices and indices for lazy factory creation
        var cVertices = new CMeshVertex[vertices.Length];
        Array.Copy(vertices, cVertices, vertices.Length);

        var cIndices = new uint[indices.Length];
        Array.Copy(indices, cIndices, indices.Length);

        FColor[]? cColors = null;
        if (lod.VertexColors is { Length: > 0 } colors)
        {
            cColors = new FColor[colors.Length];
            Array.Copy(colors, cColors, colors.Length);
        }

        FMeshUVFloat[]? cExtraUvs = null;
        if (lod.ExtraUV?.Value is { Length: > 0 } extraUvs && extraUvs[0] is { Length: > 0 } extraUv1)
        {
            cExtraUvs = new FMeshUVFloat[extraUv1.Length];
            Array.Copy(extraUv1, cExtraUvs, extraUv1.Length);
        }

        HasVertexColors = cColors != null;
        _factory = () => factory(cVertices, cIndices, cColors, cExtraUvs);
    }

    internal TPrimitiveData<TVertex> CreatePrimitive()
    {
        if (_primitive != null)
            return _primitive;

        if (_factory == null)
            throw new InvalidOperationException("Cannot create primitive: no factory available.");

        _primitive = _factory();
        return _primitive;
    }

    public void Dispose()
    {
        _primitive?.Dispose();
        _primitive = null;
    }
}
